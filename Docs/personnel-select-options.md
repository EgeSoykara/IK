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
| `PhoneTypes` | Telefonlar → Telefon Türü | Cep, iş, ev gibi telefon türleri |
| `AddressTypes` | Adresler → Adres Türü | Ev, iş, ikamet gibi adres türleri |
| `AddressHierarchy` | Adresler → Ülke, Şehir, İlçe/Bölge | Ülke altında şehirler, şehir altında ilçeler/bölgeler |
| `TerminationReasons` | İşten Ayrılma → Ayrılma Nedeni | İstifa, emeklilik gibi ayrılma nedenleri |

Listelere değer eklerken aynı görünen değeri ikinci kez eklemeyin. Boş veya
yalnız boşluk içeren seçenek kullanmayın.

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

Banka, şube, okul/kurum, bölüm/program, belgeyi düzenleyen kurum ve
kurs/sertifika düzenleyen kurum açık uçlu adlardır. Bu alanlar
`MudTextField` ile doğrudan yazılır; daha önce kaydedilmiş değerlerden seçenek
listesi üretilmez.
