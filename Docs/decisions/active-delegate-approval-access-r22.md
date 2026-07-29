# Aktif vekil izin onayı erişimi — R22

## Karar

- Bu revizyon, R19'daki manuel vekâlet devrinin `/LeaveTracking` üzerinde bulunması kararını geçersiz kılar; R19 tarihsel kayıt olarak korunur.
- `Departments.ActiveDelegateEmployeeId` üzerinde aktif vekil olarak kayıtlı çalışan, genel izin onaylama izin claim'i olmasa da `/LeaveApprovals` sayfasına erişir.
- Aktif vekilin görebileceği ve karara bağlayabileceği yönetici onayları, `LeaveRequest.ManagerApproverEmployeeId` atamasıyla sınırlı kalır. Sayfa erişimi bu kayıt kapsamını genişletmez.
- Manuel vekâlet devrinin tek kullanıcı arayüzü `/LeaveApprovals` sayfasıdır. `/LeaveTracking` yalnızca izin takvimi ve çalışma durumu takibi için kullanılır.
- Vekâlet devri sonrasında çalışanların yönetici bağlantıları ve bekleyen yönetici onayları mevcut `ManagerDelegationService` işlemiyle yeni aktif vekile atomik olarak aktarılır.
- Devreden eski vekilin onay erişimi işlemle birlikte sona erer. İşlem sonrası tam sayfa geçişi navbar yetkilerini yeniden hesaplar ve `İzin Onayları` bağlantısını eski vekilden hemen kaldırır.
- Manuel olarak ikinci vekile devredilen yetki, ikinci vekil tarafından birinci vekile geri verilebilir. Geri devir yeni ve döngüsel bir zincir kaydı oluşturmaz; son manuel devir kaydı kapatılır ve önceki vekil yeniden aktif hale getirilir.

## Kanıt hedefleri

- `Services/PageAccessService.cs` -> `CanAccessLeaveApprovalsAsync` -> aktif vekil kaydını sunucu tarafında doğrular.
- `Services/LeaveApprovalVisibilityQuery.cs` -> `VisibleTo` -> yönetici kayıtlarını atanmış çalışanla sınırlar.
- `Services/LeaveRequestService.cs` -> `ManagerDecisionAsync` -> kararı yalnız `ManagerApproverEmployeeId` ile atanmış yöneticiye kabul eder.
- `Components/Pages/LeaveApprovals.razor` -> vekâlet devri diyaloğu -> manuel devrin tek arayüzüdür.
- `Components/Pages/LeaveTracking.razor` -> izin takip arayüzü -> vekâlet devri davranışı içermez.
