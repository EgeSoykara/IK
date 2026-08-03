using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace IK.Web.Models;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class LeaveDayAmountAttribute(bool allowZero = true) : ValidationAttribute
{
    public bool AllowZero { get; } = allowZero;

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (!decimal.TryParse(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var days))
        {
            return false;
        }

        return (AllowZero ? days >= 0m : days > 0m)
            && decimal.Truncate(days * 2m) == days * 2m;
    }

    public override string FormatErrorMessage(string name) =>
        AllowZero
            ? $"{name} negatif olamaz ve yalnız tam ya da yarım gün olabilir."
            : $"{name} sıfırdan büyük olmalı ve yalnız tam ya da yarım gün olabilir.";
}
