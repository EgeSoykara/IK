# İzin Hak Edişi ve Operatör Politikası

## Kalıcı ayar yeri

İzin türlerinin çalışma zamanı otoritesi MSSQL `LeaveTypes` tablosudur. Yönetici, uygulamadaki `/LeaveTypes` sayfasından ad, yıllık kota, devir kuralı ve azami birikim uyarı değerini değiştirebilir. Sonraki politika değişiklikleri kaynak kodundan veya migration'dan değil bu sayfadan yapılmalıdır.

Temiz kurulumun ilk değerleri `Database/HumanResourcesDbContext.cs` içindeki `LeaveType` seed kayıtlarında tanımlıdır. `Database/001_create_human_resources_schema.sql` aynı temiz-kurulum verisini taşır. `Migrations/20260803072742_DailyLeaveEntitlementsAndCategories.cs` DEVELOPMENT temiz geçişidir: çalışan ve departman kayıtlarını korur; eski izin talebi, onayı, bakiyesi, izin kaynaklı vekâlet ve bunlara bağlı eski denetim kayıtlarını dönüştürmeden sıfırlar. Bu migration geriye dönüştürülemez; geri dönüş veritabanı yedeğiyle yapılır.

## Varsayılan izin türleri

| ID | İzin türü | İlk kota | Devir | Uygunluk | Azami birikim uyarısı |
|---:|---|---:|---|---|---:|
| 1 | 0-10 Yıllık Çalışan İzni | 30 gün | Evet | 10 yıldan az tamamlanmış kıdem | 50 gün |
| 2 | 10-20 Yıllık Çalışan İzni | 30 gün | Evet | 10-19 tamamlanmış kıdem yılı | 50 gün |
| 3 | 20-30 Yıllık Çalışan İzni | 30 gün | Evet | 20 yıl ve üzeri; 30+ aynı kademede devam eder | 50 gün |
| 4 | Hastalık İzni | 30 gün | Hayır | Tüm aktif çalışanlar | 50 gün |
| 5 | Hamilelik İzni | 30 gün | Hayır | Yalnız kadın çalışanlara yönetici tarafından manuel atama | 50 gün |

İzin türü kimliği pozitif SQL identity değeridir. Temiz kurulumda beş varsayılan tür 1-5 kimliklerini kullanır; sonradan oluşturulan türler 6'dan devam eder.

Tüm kota, bakiye, kullanım, devir ve talep miktarları negatif olmayan tam veya yarım gündür (`30`, `30,5`, `31`). Başka ondalıklar uygulama doğrulaması ve veritabanı check constraint'leriyle reddedilir. Otomatik kıstelyevm sonucu aşağı yuvarlanarak tam gün kaydedilir.

50 gün bir kesme sınırı değildir. Manuel işlemde sınır aşımı açık onay ister. Günlük otomatik uzlaştırma hakkı kesmeden kaydeder; devir nedeniyle sınır aşılmışsa `/LeaveCarryOverWarnings` sayfasında devreden gün, yeni hak, toplam ve uyarı sınırıyla kalıcı bir uyarı oluşturur. Bu sayfa ve onaylama işlemi sunucu tarafında `CanManageLeaveBalances` iznini gerektirir.

## Günlük otomatik uzlaştırma

`DailyLeaveEntitlementWorker` uygulama açılışında bir kez çalışır, ardından her yerel takvim gününün başlangıcında mevcut yılın otomatik haklarını uzlaştırır. Aynı çalışan, izin türü ve yıl için tek bakiye bulunur; değişmeyen değer tekrar yazılmaz, kota veya uygunluk hesabı değişmişse mevcut hak güncellenir.

Worker yalnız aktif çalışanların üç kıdem türünü ve hastalık iznini otomatik işler. Hamilelik izni hiçbir koşulda worker tarafından atanmaz; `/LeaveBalances` üzerinden yetkili yönetici tarafından manuel atanır ve servis kadın uygunluğunu yeniden doğrular. `Employee.StartDate` bulunmayan çalışan için otomatik hak tahmin edilmez ve eksik veri sayısı loglanır.

Yıl içinde işe başlayan çalışan, `StartDate` gününde 1 Ocak beklenmeden işe giriş tarihinden 31 Aralık'a kadar kalan takvim günü oranında hak kazanır. Kıdem geçişlerinde yılın her takvim günü o gündeki tamamlanmış kıdem kademesine yazılır. Her kademe için `kota × uygun takvim günü / yıldaki takvim günü` hesaplanır ve sonuç aşağı yuvarlanır. Böylece mart ayında 10. yılını dolduran çalışanın yıl başı-mart bölümü 0-10, kalan bölüm 10-20 kademesinden gelir. 30 yıl ve üzeri çalışanlar 20-30 kademesinde kalır.

Kıdem izinleri tek devir grubu sayılır. Önceki yılın üç kıdem bakiyesindeki toplam kalan, yeni yıldaki ilk uygun kıdem bakiyesine yalnız bir kez aktarılır. Hastalık ve hamilelik izinleri devretmez.

Yapılandırma anahtarları:

- `DailyLeaveEntitlementWorker__Enabled`: varsayılan `true`; operasyonel durdurma için `false`.
- `DailyLeaveEntitlementWorker__RetryDelayMinutes`: başarısız işlemden sonraki yeniden deneme süresi; varsayılan 30 dakikadır.

## Manuel atama ve izin tüketimi

`/LeaveBalances` sayfası aynı işlemi bir aktif çalışana, seçilen departmandaki aktif çalışanlara veya tüm aktif çalışanlara uygular. Hedefler sunucu tarafında çözülür; çalışan ve departman alanları isimle aranan autocomplete kontrolleridir. Ekleme, güncelleme, silme ve uyarı yönetimi servis katmanında yeniden yetkilendirilir.

Çalışan izin talebinde yalnız `Yıllık İzin` veya `Hastalık İzni` seçer. Yıllık izin üç kıdem bakiyesinin kullanılabilir toplamını tek kategori olarak gösterir. İnsan Kaynakları nihai onayında günler önce tüm kıdem bakiyelerinin devreden kısmından, ardından normal hak edişlerinden; her kaynak içinde 0-10, 10-20, 20-30+ sırasıyla düşülür. Her düşüm `LeaveRequestBalanceAllocations` tablosunda bakiye ve kaynak bazında kalıcı tutulur.

Talep günleri `LeaveDayCalculator` tarafından hesaplanır; cumartesi, pazar ve `PublicHolidays` tablosundaki resmî tatiller sayılmaz. Açık yarım gün seçimi tek uygun iş gününde `0,5` gün üretir.
