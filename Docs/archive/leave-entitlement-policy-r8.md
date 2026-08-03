# İzin Hak Edişi ve Operatör Politikası

## Kalıcı ayar yeri

İzin türlerinin çalışma zamanı otoritesi MSSQL `LeaveTypes` tablosudur. Yönetici, uygulamadaki `/LeaveTypes` sayfasından ad, yıllık kota, devir kuralı ve azami birikim uyarı değerini değiştirebilir. Dağıtım sonrasında politika değişikliği için kaynak kodu veya migration değiştirilmemeli; bu sayfa kullanılmalıdır.

Temiz veritabanının ilk değerleri `Database/HumanResourcesDbContext.cs` içindeki `LeaveType` seed kayıtlarında tanımlıdır. `Database/001_create_human_resources_schema.sql` aynı temiz-kurulum verisini, `Migrations/20260731113043_AddAutomaticLeaveEntitlements.cs` ise mevcut geliştirme veritabanlarına geçişi taşır.

## Varsayılan izin türleri

| İzin türü | İlk kota | Devir | Uygunluk | Azami birikim uyarısı |
|---|---:|---|---|---:|
| 0-10 Yıllık Çalışan İzni | 30 gün | Evet | 10 yıldan az tamamlanmış kıdem | 50 gün |
| 10-20 Yıllık Çalışan İzni | 30 gün | Evet | 10-19 tamamlanmış kıdem yılı | 50 gün |
| 20-30 Yıllık Çalışan İzni | 30 gün | Evet | 20 yıl ve üzeri; 30+ aynı kademede devam eder | 50 gün |
| Hastalık İzni | 30 gün | Hayır | Tüm aktif çalışanlar | 50 gün |
| Hamilelik İzni | 30 gün | Hayır | Yalnız `Gender = Female` olan aktif çalışanlar | 50 gün |

50 gün bir üst sınır değildir. Manuel işlemde sınır aşımı tek onay ister; otomatik işlem değeri kesmeden kaydeder ve uyarıyı işlem kaydına/loga yazar.

## Yıllık otomatik atama

`AnnualLeaveEntitlementWorker` uygulama açılışında içinde bulunulan yılın eksik hak edişlerini tamamlar ve sonraki çalışmasını yerel saate göre 1 Ocak 00:00'a kurar. Aynı çalışan, izin türü ve yıl kaydı zaten varsa değiştirmez; yeniden başlatma veya tekrar çalışma çift atama üretmez.

Worker yalnız aktif çalışanları ve otomatik hak ediş kuralı olan izin türlerini işler. Kıdem hesabının kaynağı `Employee.StartDate`, hamilelik izni uygunluğunun kaynağı `Employee.Gender` alanıdır. İşe başlama tarihi bulunmayan çalışan için kıdem izni tahmin edilmez; çalışan atlanır ve eksik veri sayısı loglanır. Cinsiyeti boş veya kadın dışında olan çalışan hamilelik izninde atlanır.

Kıdem geçişleri ay bazında oranlanır. Örneğin 10. yılını martta tamamlayan çalışan, 30 günlük yıllık kotalarda ocak-mart için 0-10 kademesinden `30 × 3 / 12 = 7,5` gün, nisan-aralık için 10-20 kademesinden `30 × 9 / 12 = 22,5` gün alır. Aynı hesap 20. yıl geçişinde de uygulanır. Kıdem izinleri tek devir grubu sayılır; önceki yıldaki kıdem bakiyelerinin toplam kalanı geçiş yılındaki ilk uygun kademeye bir kez aktarılır ve iki kademede çoğaltılmaz.

Yapılandırma anahtarları:

- `AnnualLeaveEntitlementWorker__Enabled`: varsayılan `true`; acil operasyonel durdurma için `false`.
- `AnnualLeaveEntitlementWorker__RetryDelayMinutes`: başarısız işlemden sonraki yeniden deneme süresi; varsayılan 30 dakikadır.

## Manuel atama ve gün tüketimi

`/LeaveBalances` sayfası aynı işlemi bir aktif çalışana, seçilen departmandaki aktif çalışanlara veya tüm aktif çalışanlara uygular. Hedefler sunucu tarafında çözülür; seçilen izin türünün kıdem/cinsiyet kuralına uymayan çalışanlar atlanır ve sonuç sayıları operatöre gösterilir. Ekleme, güncelleme ve silme işlemleri `CanManageLeaveBalances` iznini servis katmanında yeniden doğrular.

Hak ediş, takvim günü sayısıdır. İzin talebi bakiyeden düşülürken `LeaveDayCalculator` cumartesi, pazar ve `PublicHolidays` tablosunda tanımlı resmî tatilleri saymaz. Bu iki davranışın otoritesi ayrıdır: worker yıllık hakkı atar, izin talebi servisi kullanılan iş gününü hesaplar.
