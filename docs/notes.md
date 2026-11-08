# InvoiceAI: test qeydləri

## Modellər
- Chat modeli: gpt-6-luna (OpenAI), reasoning modeli
  - Qiymət: giriş $0.10 / 1M token, çıxış $0.50 / 1M token
  - Temperature: yalnız default (1) dəstəklənir, 0 göndərəndə xəta verir.
    Sabitlik structured output + sistem promptundakı qaydalar + koddakı validasiya ilə təmin olunur.
- Embedding modeli: text-embedding-3-small
  - Qiymət: $0.02 / 1M token, ölçü: 1536
  - Large ($0.13, 3072 ölçü) əvəzinə seçildi: qısa sənədlər üçün keyfiyyət fərqi azdır, 6.5 dəfə ucuzdur.

## Addım 1: ilk təcrübələr
- Eyni sual 3 dəfə: ilk cümlələr eyni, qalanı fərqli (model ehtimalla seçir).
- Eyni sual EN və AZ: 34 və 36 giriş tokeni.
- "Ferrum Capital-ın 2025 gəliri" sualı: model uydurmadı, "etibarlı məlumatım yoxdur" dedi.
- AZ dilində 2 cümləlik cavab: 107 çıxış tokeni.

## Addım 2: faktura çıxarma nəticələri

| Faktura | Nəticə | Tokenlər (giriş / çıxış) | Xərc | Vaxt |
| --- | --- | --- | --- | --- |
| 01 (EN) | Bütün sahələr düzgün | 620 / 259 | ≈ $0.00019 | 4.1 s |
| 02 (AZ, "33 984,00" formatı) | Bütün sahələr düzgün | 686 / 306 | ≈ $0.00022 | 4.5 s |
| 03 (ödəniş tarixi yazılmayıb, "45 gün") | Bütün sahələr düzgün, tarix hesablandı: 2026-09-17 | 581 / 258 | ≈ $0.00019 | 3.4 s |

- Bir faktura ≈ 0.02 sent və ≈ 4 saniyə. 1 000 faktura ≈ 20 sent.
- Müqayisə: əllə köçürmə bir faktura üçün 3–5 dəqiqədir.

## Addım 2.10: PostgreSQL və validasiya
- Baza: PostgreSQL 17 + pgvector, Docker Compose ilə (port 5433).
- 3 faktura bazaya yazıldı, hamısı "Processed" statusunda.
- Validasiya testi: sistem promptuna müvəqqəti olaraq "totalAmount = 1.00" qaydası əlavə edildi.
  Nəticə: "Subtotal + VAT does not match total" xəbərdarlığı, status "NeedsReview".
  Deməli model səhv etsə belə sistem onu tutur və insan yoxlamasına göndərir (human-in-the-loop).

## Addım 3A: semantic search, ilk ölçmə (chunk = 800 simvol)
Hər faktura 1 chunk oldu (mətn 800 simvoldan qısadır).

| Sorğu | Birinci nəticə | Distance | Qiymət |
| --- | --- | --- | --- |
| late payment penalty | INV-2026-0451 | 0.723 | Düzgün, amma 2-ci ilə fərq çox kiçik (0.742) |
| gecikmə cəriməsi | AQ-0917 | 0.672 | AZ sorğu AZ fakturanı seçdi |
| ərzaq məhsulları | AQ-0917 | 0.617 | Düzgün, amma satıcı adında "Məhsulları" sözü var |
| Nord Trade faiz dərəcəsi | AQ-0917 | 0.457 | Səhv: faiz NTS/26/1183-dədir |
| futbol matçının nəticəsi | AQ-0917 | 0.698 | Uyğunsuz sual düzgün cavabdan daha yaxın çıxdı |

Nəticələr:
1. Dil təsiri: AZ sorğular demək olar ki, həmişə AZ sənədi birinci qaytarır.
2. Bütöv sənədin bir vektoru mənaları qarışdırır, konkret bənd (cərimə, faiz) itir.
3. Distance üzrə sabit hədd qoymaq olmur: uyğunsuz sual 0.698, düzgün cavab 0.723.

Növbəti təcrübə: chunk ölçüsünü 300 simvola endirmək.