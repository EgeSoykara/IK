# Personel Bilgileri Excel arayüzü kaldırma — R24

## Karar

- `Personel Bilgileri` menüsü altındaki tüm sayfalarda Excel içe aktarma ve dışa aktarma kontrolleri kaldırılmıştır.
- Kapsam; personel özeti, banka, genel bilgiler, kimlik-belge, telefon, adres, eğitim, kurs-sertifika ve işten ayrılma sekmeleridir.
- Bu sayfalardaki manuel ekleme, düzenleme, silme ve ilişkili belge işlemleri değişmeden kalır.
- Ana `Çalışanlar` ve `Resmî Tatiller` sayfaları bu menü grubunun dışında olduğundan mevcut Excel işlemlerini korur.
- Bu değişiklik veri modelini veya veritabanı şemasını değiştirmez; migration gerektirmez.

## Kanıt hedefleri

- `Tests/PersonnelInformationUiContractTests.cs` -> `PersonnelInformationPages_DoNotExposeExcelActions` -> dokuz personel alt sayfasında Excel bileşeni bulunmadığını, yalnız `Employees` ve `PublicHolidays` sayfalarında kaldığını doğrular.
- `.bet-task/e2e/employee-information-e2e.mjs` -> `personnelRoutesWithoutExcel` -> gerçek tarayıcıda alt sayfalarda export düğmesi ve XLSX dosya girişi bulunmadığını doğrular.
