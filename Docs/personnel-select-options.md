# Personel Select Seçenekleri

Personel formlarındaki kategorik alanların seçenek otoritesi
`Components/PersonnelSelectOptions.cs` dosyasıdır. Bu alanlar serbest metin
kabul etmez ve daha önce kaydedilmiş değerlerden kendiliğinden yeni seçenek
üretmez.

## Elle doldurulacak alanlar

| Kod bölümü | Kullanıldığı alan | Beklenen içerik |
| --- | --- | --- |
| `DocumentTypes` | Kimlik ve Belgeler → Belge Türü | Pasaport, kimlik kartı gibi belge türleri |
| `EducationLevels` | Eğitimler → Eğitim Seviyesi | Doktora, yüksek lisans, lisans, ön lisans gibi seviyeler |
| `PhoneTypes` | Telefonlar → Telefon Türü | AD `telephoneNumber` eşlemesi için kanonik `İş`; operatör onaylı Cep, Ev gibi ek türler |
| `AddressTypes` | Adresler → Adres Türü | Ev, iş, ikamet gibi adres türleri |
| `AddressHierarchy` | Adresler → Ülke, Şehir, İlçe/Bölge | Ülke altında şehirler, şehir altında ilçeler/bölgeler |

Listelere değer eklerken aynı görünen değeri ikinci kez eklemeyin. Boş veya
yalnız boşluk içeren seçenek kullanmayın.

`İş` değeri `Models/EmployeePhoneTypes.cs` içinde tanımlanan sistem değeridir.
AD otomatik çalışan oluşturma akışı, mevcut `telephoneNumber` değerini bu türde
birincil telefon olarak ekler; bu nedenle `PhoneTypes` listesinden çıkarılmaz.

Adres seçenekleri tek bir hiyerarşide tutulur. Örnek biçim:

```csharp
new(
    "Ülke",
    [
        new("Şehir", ["İlçe/Bölge"])
    ])
```

Form adresi şu sırayla doldurur:

1. Adres türü
2. Ülke
3. Şehir
4. İlçe/Bölge
5. Posta kodu
6. Açık adres
7. Birincil adres seçimi

Ülke seçilmeden şehir, şehir seçilmeden ilçe/bölge etkinleşmez. Ülke
değiştiğinde şehir ve ilçe; şehir değiştiğinde ilçe temizlenir. İlgili seçenek
listesi boşsa alan devre dışı görünür ve kullanıcıya seçeneklerin henüz
tanımlanmadığı açıklanır.

## Serbest metin alanları

Banka, şube, okul/kurum, bölüm/program, belgeyi düzenleyen kurum,
kurs/sertifika düzenleyen kurum ve işten ayrılma nedeni açık uçlu adlardır.
İşten ayrılma nedeni, günlük kullanımı hızlandırmak için mevcut kayıtlardaki
benzersiz değerleri autocomplete önerisi olarak sunar; yeni değer yazılmasına
izin verir ve bu öneriler ayrı bir referans veri otoritesi oluşturmaz. Diğer
açık uçlu alanlar `MudTextField` ile doğrudan yazılır.
