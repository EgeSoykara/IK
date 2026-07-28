# Personel Excel Import / Export

`Import Excel` ve `Export Excel` düğmeleri mevcut tekil ekleme, düzenleme ve silme işlemlerini değiştirmez. Excel işlemleri şu sayfalardadır:

- Resmî Tatiller
- Çalışanlar
- Banka Bilgileri
- Kimlik ve Belgeler
- Telefonlar
- Adresler
- Eğitimler
- Kurs ve Sertifikalar

Genel Bilgiler ve İşten Ayrılma sayfalarında Excel işlemi yoktur.

## Çalışma biçimi

- Yalnız makro içermeyen `.xlsx` kabul edilir.
- En fazla 5 MiB ve 2.000 veri satırı kabul edilir.
- Dosyada tek çalışma sayfası ve aşağıdaki başlıkların aynı sırada bulunması gerekir.
- Formül ve dış bağlantı içeren dosyalar reddedilir.
- Import yalnız yeni kayıt ekler; mevcut kaydı güncellemez.
- Dosya içi veya sistemde mevcut mükerrer kayıt reddedilir.
- Herhangi bir satır hatalıysa hiçbir satır kaydedilmez. Hata satır ve sütun adıyla gösterilir.
- Tarihler gerçek Excel tarihi veya `yyyy-MM-dd` metni olabilir. Export tarihleri gerçek Excel tarihi olarak yazar.
- Boolean sütunlar `Evet` veya `Hayır` olmalıdır.
- Personel alt sayfalarında işlem yalnız seçili ve yetkili çalışanın kayıtlarını kapsar. Resmî Tatiller seçili yılı kapsar. Çalışanlar tüm yetkili çalışan listesini kapsar.
- Import/export yetkisi sunucuda tekrar doğrulanır; yalnız düğmenin görünmesi yetki sağlamaz.

## Başlıklar

| Sayfa | Sütunlar |
| --- | --- |
| Resmî Tatiller | Tarih, Tatil Adı |
| Çalışanlar | Sicil No, Ad, Soyad, KKTC Kimlik No, Departman, İşe Başlama Tarihi, Kadro Tarihi, Cinsiyet, Kan Grubu, Durum |
| Banka Bilgileri | Banka, Şube, Şube Kodu, Hesap Numarası, IBAN, Birincil |
| Kimlik ve Belgeler | Belge Türü, Belge Numarası, Düzenleyen Kurum, Düzenlenme Tarihi, Son Geçerlilik Tarihi, Açıklama |
| Telefonlar | Telefon Türü, Telefon Numarası, Dahili, Birincil |
| Adresler | Adres Türü, Ülke, Şehir, İlçe/Bölge, Posta Kodu, Adres, Birincil |
| Eğitimler | Kurum/Okul, Program/Bölüm, Eğitim Seviyesi, Diploma/Derece, Başlangıç Tarihi, Mezuniyet Tarihi, Mezun |
| Kurs ve Sertifikalar | Kurs/Sertifika Adı, Düzenleyen Kurum, Başlangıç Tarihi, Bitiş Tarihi, Belge Numarası, Geçerlilik Tarihi |

Kontrollü seçenekler (`Belge Türü`, `Telefon Türü`, `Adres Türü`, adres hiyerarşisi ve `Eğitim Seviyesi`) `Components/PersonnelSelectOptions.cs` içindeki değerlerle birebir aynı olmalıdır. Seçeneklerin elle girileceği yerler ayrıca `Docs/personnel-select-options.md` dosyasında açıklanır.
