using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using Bunit;
using IK.Web.Components.Pages;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace IK.Web.Tests;

public sealed class ReportedRegressionBehaviorTests
{
    [Fact]
    public async Task Employees_OverlappingTableLoads_UseIndependentFactoryContexts()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(
                $"employee-table-overlap-{Guid.NewGuid():N}",
                new InMemoryDatabaseRoot())
            .Options;
        var factory = new OverlapDbContextFactory(options);
        var component = new Employees
        {
            DatabaseFactory = factory
        };
        var loadMethod = typeof(Employees).GetMethod(
            "LoadEmployeesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(loadMethod);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var state = new TableState { Page = 0, PageSize = 25 };

        var firstLoad = InvokeLoadAsync(component, loadMethod, state, cancellation.Token);
        await factory.FirstCreateEntered.WaitAsync(cancellation.Token);
        var quickSearchReload = InvokeLoadAsync(component, loadMethod, state, cancellation.Token);

        var results = await Task.WhenAll(firstLoad, quickSearchReload);

        Assert.Equal(2, factory.CreateCount);
        Assert.Equal(2, factory.DistinctContextCount);
        Assert.All(factory.ReceivedTokens, token => Assert.Equal(cancellation.Token, token));
        Assert.All(results, result =>
        {
            Assert.NotNull(result.Items);
            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalItems);
        });
    }

    [Fact]
    public void LeaveRequestReason_InputImmediatelyReachesModelBeforeSubmitValidation()
    {
        using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<ImmediateReasonFormHarness>();

        component.Find("textarea").Input("Planlı yıllık izin");
        component.Find("form").Submit();

        Assert.Equal("Planlı yıllık izin", component.Instance.Model.Reason);
        Assert.Equal(1, component.Instance.ValidSubmitCount);
        Assert.DoesNotContain(
            "İzin talep nedeni zorunludur.",
            component.Markup,
            StringComparison.Ordinal);
    }

    private static Task<TableData<Employee>> InvokeLoadAsync(
        Employees component,
        MethodInfo loadMethod,
        TableState state,
        CancellationToken cancellationToken) =>
        Assert.IsAssignableFrom<Task<TableData<Employee>>>(
            loadMethod.Invoke(component, [state, cancellationToken]));

    private sealed class OverlapDbContextFactory(
        DbContextOptions<HumanResourcesDbContext> options)
        : IDbContextFactory<HumanResourcesDbContext>
    {
        private readonly TaskCompletionSource _firstCreateEntered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstCreate = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<HumanResourcesDbContext> _contexts = [];
        private readonly List<CancellationToken> _receivedTokens = [];
        private int _createCount;

        public Task FirstCreateEntered => _firstCreateEntered.Task;
        public int CreateCount => Volatile.Read(ref _createCount);
        public int DistinctContextCount
        {
            get
            {
                lock (_contexts)
                {
                    return _contexts.Distinct(ReferenceEqualityComparer.Instance).Count();
                }
            }
        }

        public IReadOnlyList<CancellationToken> ReceivedTokens
        {
            get
            {
                lock (_receivedTokens)
                {
                    return [.. _receivedTokens];
                }
            }
        }

        public HumanResourcesDbContext CreateDbContext() => CreateContext();

        public async Task<HumanResourcesDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            var callNumber = Interlocked.Increment(ref _createCount);
            lock (_receivedTokens)
            {
                _receivedTokens.Add(cancellationToken);
            }

            if (callNumber == 1)
            {
                _firstCreateEntered.TrySetResult();
                await _releaseFirstCreate.Task.WaitAsync(cancellationToken);
            }
            else
            {
                _releaseFirstCreate.TrySetResult();
            }

            return CreateContext();
        }

        private HumanResourcesDbContext CreateContext()
        {
            var context = new HumanResourcesDbContext(options);
            lock (_contexts)
            {
                _contexts.Add(context);
            }

            return context;
        }
    }
}

public sealed class ImmediateReasonFormHarness : ComponentBase
{
    public ReasonFormModel Model { get; } = new();
    public int ValidSubmitCount { get; private set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, nameof(EditForm.Model), Model);
        builder.AddAttribute(
            2,
            nameof(EditForm.OnValidSubmit),
            EventCallback.Factory.Create<EditContext>(this, _ => ValidSubmitCount++));
        builder.AddAttribute(3, nameof(EditForm.ChildContent), (RenderFragment<EditContext>)(_ => contentBuilder =>
        {
            contentBuilder.OpenComponent<DataAnnotationsValidator>(0);
            contentBuilder.CloseComponent();
            contentBuilder.OpenComponent<MudTextField<string>>(1);
            contentBuilder.AddAttribute(2, nameof(MudTextField<string>.Value), Model.Reason);
            contentBuilder.AddAttribute(
                3,
                nameof(MudTextField<string>.ValueChanged),
                EventCallback.Factory.Create<string>(this, value => Model.Reason = value));
            contentBuilder.AddAttribute(
                4,
                nameof(MudTextField<string>.For),
                (Expression<Func<string>>)(() => Model.Reason));
            contentBuilder.AddAttribute(5, nameof(MudTextField<string>.Immediate), true);
            contentBuilder.AddAttribute(6, nameof(MudTextField<string>.Required), true);
            contentBuilder.AddAttribute(
                7,
                nameof(MudTextField<string>.RequiredError),
                "İzin talep nedeni zorunludur.");
            contentBuilder.AddAttribute(8, nameof(MudTextField<string>.Lines), 4);
            contentBuilder.CloseComponent();
            contentBuilder.OpenElement(9, "button");
            contentBuilder.AddAttribute(10, "type", "submit");
            contentBuilder.AddContent(11, "Gönder");
            contentBuilder.CloseElement();
        }));
        builder.CloseComponent();
    }
}

public sealed class ReasonFormModel
{
    [Required(ErrorMessage = "İzin talep nedeni zorunludur.")]
    public string Reason { get; set; } = string.Empty;
}
