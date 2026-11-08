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

## Addım 3A: semantic search

### Ölçmə 1: chunk = 800 simvol (hər faktura 1 chunk)

| Sorğu | Birinci nəticə | Distance | Qiymət |
| --- | --- | --- | --- |
| late payment penalty | INV-2026-0451 | 0.723 | Düzgün, amma 2-ci ilə fərq çox kiçik (0.742) |
| gecikmə cəriməsi | AQ-0917 | 0.672 | AZ sorğu AZ fakturanı seçdi |
| ərzaq məhsulları | AQ-0917 | 0.617 | Düzgün, amma satıcı adında "Məhsulları" sözü var |
| Nord Trade faiz dərəcəsi | AQ-0917 | 0.457 | NTS/26/1183 tapılmadı |
| futbol matçının nəticəsi | AQ-0917 | 0.698 | Uyğunsuz sual düzgün cavabdan daha yaxın çıxdı |

### Ölçmə 2: chunk = 300 simvol (9 chunk)

| Sorğu | Birinci nəticə | Distance | Qiymət |
| --- | --- | --- | --- |
| late payment penalty | INV-2026-0451 #2 | 0.576 | Yaxşılaşdı: dəqiq cərimə bəndi tapıldı |
| gecikmə cəriməsi | AQ-0917 #1 | 0.657 | Düzgün, amma EN fakturalardakı cərimələr top 3-də yoxdur |
| interest on overdue balance | INV-2026-0451 #2 | 0.607 | Düzgün cavab (NTS, 0.609) ikinci oldu |
| Nord Trade faiz dərəcəsi | AQ-0917 | 0.458 | NTS hələ də top 3-də yoxdur |
| food products | AQ-0917 #0 | 0.799 | Düzgün, amma "Araz Qida Məhsulları" hərfən "food products"-dur |
| futbol matçının nəticəsi | AQ-0917 #1 | 0.710 | Düzgün cavabdan (0.799) yaxın çıxdı |

Tapılan xəta: chunker sözləri ortadan kəsirdi ("iymət Məbləğ"). Sətir əsaslı chunker ilə düzəldildi.

Nəticələr:
1. Kiçik chunk eyni dildə işləyir: dəqiq bəndi tapır, distance azalır.
2. Dillər arası axtarış zəifdir: AZ sorğu EN sənədi tapmır və əksinə.
3. Distance üzrə sabit hədd qoymaq olmur. "Tapılmadı" qərarını LLM verməlidir.
4. Nord Trade iki rolda çıxış edir (AQ-0917-də alıcı, NTS-də satıcı). Düzgün cavab ikisini də göstərməlidir.

## Addım 3B: RAG (multi-query + mənbə istinadı)
Axın: LLM sualı EN və AZ sorğulara çevirir → 3 sorğu ilə axtarış → top 6 chunk →
LLM yalnız bu mənbələrə əsaslanıb cavab verir, mənbə id-lərini qaytarır, cavab yoxdursa found = false.

| # | Sual | Nəticə | Tokenlər | Vaxt |
| --- | --- | --- | --- | --- |
| 1 | Nord Trade-in faiz/cərimə dərəcəsi | Düzgün: NTS 1.5%/ay (satıcı), AQ 0.05%/gün (alıcı) | 1151 / 342 | 7.0 s |
| 2 | Xazar Metal gecikdirsə nə qədər faiz | Düzgün: NTS, 1.5%/ay (Xazar orada alıcıdır) | 919 / 241 | 5.2 s |
| 3 | What food products were sold and to whom? (EN) | Düzgün: yağ və düyü, Nord Trade-ə; cavab ingiliscə | 1087 / 125 | 3.9 s |
| 4 | Hansı fakturalar Ferrum Capital-a təhvil verilib | Düzgün: INV-2026-0451 | 1016 / 150 | 4.3 s |
| 5 | Futbol matçının nəticəsi | Düzgün: found = false | 1167 / 127 | 3.7 s |
| 6 | Hansı fakturaların ödəniş tarixi keçib | Uğursuz (gözlənilən) | 1114 / 414 | 7.5 s |

- Multi-query dil problemini həll etdi: NTS 1-ci sualda S1 oldu (0.445).
- Bir sual ≈ $0.0003, 4–7 saniyə (iki LLM çağırışı: trade-off).
- 6-cı sual niyə uğursuz oldu:
  - Model yalnız 6 parçanı görür; tarixləri olan parçalar onların arasında yox idi.
  - Bugünkü tarix verilməmişdi, model onu fərz etdi (gizli fərziyyə, maliyyədə qəbuledilməz).
  - Düzgün cavab (2026-11-08): INV-2026-0451 və NTS/26/1183.
- Qərar: tarix, məbləğ, sayma suallarına RAG yox, bazaya strukturlaşdırılmış sorğu lazımdır → Addım 4 (tool calling).