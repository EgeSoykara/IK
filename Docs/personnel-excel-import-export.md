# Personel Excel Import / Export

`İçe Aktar` ve `Dışa Aktar` düğmeleri mevcut tekil ekleme, düzenleme ve silme işlemlerini değiştirmez. Excel işlemleri yalnız şu sayfalardadır:

- Resmî Tatiller
- Çalışanlar

`Personel Bilgileri` menüsü altındaki personel özeti, banka, genel bilgiler, kimlik-belge, telefon, adres, eğitim, kurs-sertifika ve işten ayrılma sayfalarında Excel işlemi yoktur. Bu sayfalardaki kayıtlar manuel arayüzlerden yönetilir.

## Çalışma biçimi

- Yalnız makro içermeyen `.xlsx` kabul edilir.
- En fazla 5 MiB ve 2.000 veri satırı kabul edilir.
- Dosyada tek çalışma sayfası ve aşağıdaki başlıkların aynı sırada bulunması gerekir.
- Formül ve dış bağlantı içeren dosyalar reddedilir.
- Import yalnız yeni kayıt ekler; mevcut kaydı güncellemez.
- Dosya içi veya sistemde mevcut mükerrer kayıt reddedilir.
- Herhangi bir satır hatalıysa hiçbir satır kaydedilmez. Hata satır ve sütun adıyla gösterilir.
- Tarihler gerçek Excel tarihi veya `yyyy-MM-dd` metni olabilir. Export tarihleri gerçek Excel tarihi olarak yazar.
- Resmî Tatiller işlemi seçili yılı kapsar. Çalışanlar işlemi tüm yetkili çalışan listesini kapsar.
- Import/export yetkisi sunucuda tekrar doğrulanır; yalnız düğmenin görünmesi yetki sağlamaz.

## Başlıklar

| Sayfa | Sütunlar |
| --- | --- |
| Resmî Tatiller | Tarih, Tatil Adı |
| Çalışanlar | Sicil No, Ad, Soyad, E-posta, KKTC Kimlik No, Departman, Rol, İşe Başlama Tarihi, Kadro Tarihi, Cinsiyet, Kan Grubu, Durum |

Çalışan içe aktarımında e-posta zorunlu, normalize edilmiş ve benzersizdir. Her yeni çalışan için KKTC kimlik numarasından hash'lenen geçici credential aynı transaction içinde oluşturulur; çalışan ilk girişte farklı bir şifre belirlemeden uygulama sayfalarına erişemez.

Çalışan e-postası zorunlu, geçerli ve sistem genelinde benzersiz olmalıdır. Rol zorunludur ve Excel'deki rol adı `ApplicationRoles` tablosunda mevcut olmalıdır.
