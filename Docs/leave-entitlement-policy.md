# İzin Hak Edişi ve Operatör Politikası

## Kalıcı ayar yeri

İzin türlerinin çalışma zamanı otoritesi MSSQL `LeaveTypes` tablosudur. Yönetici, uygulamadaki `/LeaveTypes` sayfasından ad, yıllık kota, devir kuralı ve azami birikim uyarı değerini değiştirebilir. Seferberlik İzni bu genel uyarı politikasının istisnasıdır: devredemez ve yıllık kota/azami gün hiçbir onayla 2'yi aşamaz. Sonraki genel politika değişiklikleri kaynak kodundan veya migration'dan değil bu sayfadan yapılmalıdır.

Temiz kurulumun ilk değerleri `Database/HumanResourcesDbContext.cs` içindeki `LeaveType` seed kayıtlarında tanımlıdır. `Database/001_create_human_resources_schema.sql` aynı temiz-kurulum verisini taşır. `Migrations/20260803072742_DailyLeaveEntitlementsAndCategories.cs` ve `AddBalanceBackedLeaveRequestsAndMobilization` DEVELOPMENT temiz geçişleridir: çalışan ve departman kayıtlarını korur; eski izin talebi, onayı, bakiyesi, izin kaynaklı vekâlet ve bunlara bağlı eski denetim kayıtlarını dönüştürmeden sıfırlar. İkinci geçiş aktif vekâlet varsa çalışanların yönetici bağlarını önce asıl departman yöneticisi otoritesine geri döndürür; asıl yönetici çözülemiyorsa veri zincirini sessizce bozmadan geçişi reddeder. Ayrıca manuel türlerin 6 kimliğiyle ve eski sabit Hastalık kategorisi taleplerinin yeni FK/check sözleşmesiyle çakışmasını önlemek için altı varsayılan türü tek seferde 1-6 olarak yeniden kurar. Bu geçişler geriye dönüştürülemez; geri dönüş veritabanı yedeğiyle yapılır.

## Varsayılan izin türleri

| ID | İzin türü | İlk kota | Devir | Uygunluk | Azami birikim uyarısı |
|---:|---|---:|---|---|---:|
| 1 | 0-10 Yıllık Çalışan İzni | 30 gün | Evet | 10 yıldan az tamamlanmış kıdem | 50 gün |
| 2 | 10-20 Yıllık Çalışan İzni | 30 gün | Evet | 10-19 tamamlanmış kıdem yılı | 50 gün |
| 3 | 20-30 Yıllık Çalışan İzni | 30 gün | Evet | 20 yıl ve üzeri; 30+ aynı kademede devam eder | 50 gün |
| 4 | Hastalık İzni | 30 gün | Hayır | Tüm aktif çalışanlar | 50 gün |
| 5 | Hamilelik İzni | 30 gün | Hayır | Yalnız kadın çalışanlara yönetici tarafından manuel atama | 50 gün |
| 6 | Seferberlik İzni | 2 gün | Hayır | Yalnız aktif erkek çalışanlara otomatik atama | 2 gün |

İzin türü kimliği pozitif SQL identity değeridir. Temiz kurulumda altı varsayılan tür 1-6 kimliklerini kullanır; sonradan oluşturulan türler 7'den devam eder.

Tüm kota, bakiye, kullanım, devir ve talep miktarları negatif olmayan tam veya yarım gündür (`30`, `30,5`, `31`). Başka ondalıklar uygulama doğrulaması ve veritabanı check constraint'leriyle reddedilir. Otomatik kıstelyevm sonucu aşağı yuvarlanarak tam gün kaydedilir.

50 gün genel izin türleri için bir kesme sınırı değildir. Manuel işlemde sınır aşımı açık onay ister. Seferberlik İzni'nin 2 günlük sınırı ise serttir; uyarı onayı bunu aşamaz. Günlük otomatik uzlaştırma genel hakkı kesmeden kaydeder; devir nedeniyle sınır aşılmışsa `/LeaveCarryOverWarnings` sayfasında devreden gün, yeni hak, toplam ve uyarı sınırıyla kalıcı bir uyarı oluşturur. Yetkili yönetici bekleyen satırdaki tek `İncele` eylemiyle devreden günü negatif olmayan tam/yarım gün olarak seçer; onay aynı atomik işlemde gerçek `LeaveBalance` kaydını ve kalan günü günceller ve uyarıyı `İncelendi` durumuna taşır. Her onay denemesi kısa ömürlü ayrı bir veritabanı bağlamında çalışır; eşzamanlı değişiklik satır sürümüyle reddedilir ve yenilenen değerlerle yapılan sonraki deneme önceki başarısız değişiklikleri veya audit kaydını taşımaz. Devreden gün düzeltmesi, güncel `UsedDays` otoritesine göre kalan bakiyeyi negatif yapamaz; geçmiş kaynak tahsisleri bu güncel kapasite hesabına alt sınır koymaz. İncelenmiş karar, seçilen toplam uyarı sınırının altında veya üstünde olsa da geçmiş karar olarak korunur; bakiye daha sonra yeniden sınırı aşan farklı bir değere çıkarsa otomatik uzlaştırma aynı kaydı tekrar `Bekleyen` durumuna alır. Sayfa ve inceleme işlemi sunucu tarafında `CanManageLeaveBalances` iznini gerektirir ve audit kaydı üretir.

## Günlük otomatik uzlaştırma

`DailyLeaveEntitlementWorker` uygulama açılışında bir kez çalışır, ardından her yerel takvim gününün başlangıcında mevcut yılın otomatik haklarını uzlaştırır. Aynı çalışan, izin türü ve yıl için tek bakiye bulunur; değişmeyen değer tekrar yazılmaz, kota veya uygunluk hesabı değişmişse mevcut hak güncellenir.

Worker yalnız aktif çalışanların üç kıdem türünü, hastalık iznini ve Seferberlik İzni'ni otomatik işler. Seferberlik bakiyesi her takvim yılında toplam 2 gündür, devretmez ve yalnız `Employee.Gender = Male` çalışanlara atanır. Hamilelik izni hiçbir koşulda worker tarafından atanmaz; `/LeaveBalances` üzerinden yetkili yönetici tarafından manuel atanır ve servis kadın uygunluğunu yeniden doğrular. `Employee.StartDate` bulunmayan çalışan için kıdem/hastalık hakkı tahmin edilmez ve eksik veri sayısı loglanır.

Yıl içinde işe başlayan çalışan, `StartDate` gününde 1 Ocak beklenmeden işe giriş tarihinden 31 Aralık'a kadar kalan takvim günü oranında hak kazanır. Kıdem geçişlerinde yılın her takvim günü o gündeki tamamlanmış kıdem kademesine yazılır. Her kademe için `kota × uygun takvim günü / yıldaki takvim günü` hesaplanır ve sonuç aşağı yuvarlanır. Böylece mart ayında 10. yılını dolduran çalışanın yıl başı-mart bölümü 0-10, kalan bölüm 10-20 kademesinden gelir. 30 yıl ve üzeri çalışanlar 20-30 kademesinde kalır.

Kıdem izinleri tek devir grubu sayılır. Önceki yılın üç kıdem bakiyesindeki toplam kalan, yeni yıldaki ilk uygun kıdem bakiyesine yalnız bir kez aktarılır. Hastalık ve hamilelik izinleri devretmez.

Yapılandırma anahtarları:

- `DailyLeaveEntitlementWorker__Enabled`: varsayılan `true`; operasyonel durdurma için `false`.
- `DailyLeaveEntitlementWorker__RetryDelayMinutes`: başarısız işlemden sonraki yeniden deneme süresi; varsayılan 30 dakikadır.

## Manuel atama ve izin tüketimi

`/LeaveBalances` sayfası aynı işlemi bir aktif çalışana, seçilen departmandaki aktif çalışanlara veya tüm aktif çalışanlara uygular. Hedefler sunucu tarafında çözülür; çalışan ve departman alanları isimle aranan autocomplete kontrolleridir. Ekleme, güncelleme, silme ve uyarı yönetimi servis katmanında yeniden yetkilendirilir.

Bakiye düzenleme penceresi hak edilen, devreden ve kullanılan günleri birlikte düzeltir. `LeaveBalance.UsedDays` güncel kullanım ve kapasite otoritesidir; her gerçek düzeltmede kalan gün yeniden `hak edilen + devreden - kullanılan` olarak hesaplanır ve eski/yeni kullanım değeri audit kaydına yazılır. Değerleri değiştirmeyen kayıt işlemi audit veya satır sürümü üretmez. Onaylı taleplerin `LeaveRequestBalanceAllocations` kayıtları yalnızca değişmez onay kaynak provenansı olarak kalır; güncel kapasiteyi sınırlamaz ve toplam tahsisin düzeltilmiş kullanılan güne eşit olması gerekmez. Sonraki İK onayları düzeltilmiş kullanılan güne eklenir ve kendi kaynak tahsislerini oluşturmaya devam eder.

Çalışan izin talebinde, talep yılı için pozitif bakiyesi varsa `Yıllık İzin` ile tüm kalıcı non-service izin türlerini seçebilir. Buna yönetici tarafından sonradan oluşturulan `Manual` türler, Hastalık, uygunlukla atanmış Hamilelik ve Seferberlik izinleri dahildir. Bakiyesi olmayan tür UI'da sunulmaz; backend seçilen türün yıl bakiyesini, pozitif gün yeterliliğini ve cinsiyet uygunluğunu yeniden doğrular. Yıllık izin üç kıdem bakiyesinin kullanılabilir toplamını tek kategori olarak gösterir. İnsan Kaynakları nihai onayında yıllık izin günleri önce tüm kıdem bakiyelerinin devreden kısmından, ardından normal hak edişlerinden; her kaynak içinde 0-10, 10-20, 20-30+ sırasıyla düşülür. Diğer talepler yalnız seçilen `LeaveTypeId` bakiyesinden düşülür. Her düşüm `LeaveRequestBalanceAllocations` tablosunda bakiye ve kaynak bazında kalıcı tutulur.

Talep günleri `LeaveDayCalculator` tarafından hesaplanır; cumartesi, pazar ve `PublicHolidays` tablosundaki resmî tatiller sayılmaz. Açık yarım gün seçimi tek uygun iş gününde `0,5` gün üretir.

`/LeaveTracking` aynı `PublicHolidays` otoritesini ay snapshot'ına ad ve tarih olarak taşır. Resmî tatil günü takvimde mavi tatil yüzeyiyle ve tatil adıyla gösterilir; departman filtresi tatil görünürlüğünü değiştirmez.
