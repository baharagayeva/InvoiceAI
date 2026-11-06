# InvoiceAI: test qeydləri

## Model
- Model: gpt-6-luna (OpenAI), reasoning modeli
- Qiymət: giriş $0.10 / 1M token, çıxış $0.50 / 1M token
- Temperature: yalnız default (1) dəstəklənir, 0 göndərəndə xəta verir.
  Sabitlik structured output + sistem promptundakı qaydalar + koddakı validasiya ilə təmin olunur.

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

## Müşahidələr
- Eyni şirkət fərqli adlarla gəlir ("Nord Trade Solutions MMC" / "LLC"), VÖEN isə eynidir.
  Qərar: şirkətləri ada görə yox, VÖEN-ə görə birləşdirmək.
- Məhdudiyyət: PdfPig yalnız mətni seçilə bilən PDF-ləri oxuyur. Skan olunmuş PDF üçün OCR lazımdır.