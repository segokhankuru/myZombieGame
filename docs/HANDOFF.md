# Handoff — 2026-08-31

Oturum kapatılıyor. Bir sonraki oturumun ilk okuyacağı özet.

---

## Nerede kaldık

**M-00 kapandı** (4/4 iş, 4/4 çıkış kriteri). **M-01'de 12 işten 2'si bitti.**

| İş | Durum |
|---|---|
| M1-01 Tur ölçekleme | ✅ 12 test yeşil |
| M1-02 Ekonomi | ✅ 18 test yeşil |
| M1-03 Gri kutu harita | 🔧 üreteç yazıldı, sahne kuruldu, NavMesh bake edildi. **Ölçü ayarı geliştiricide** |
| M1-04 Zombi | ⬅ **sıradaki** |

Toplam 37 EditMode testi, hepsi yeşil. Çalışma ağacı temiz.

## Sıradaki iş — M1-04 zombi

`Bunker.AI` içinde: NavMesh ile kovalama, pencereden içeri tırmanma, can, ölüm,
zaman dilimli düşünme.

**M0-04 ölçümünün doğrudan sonucu:** 15 m üstündeki zombilerde
`Animator.cullingMode = CullCompletely` **baştan** konulacak. Ölçüm riskin NavMesh'te
değil animatörde olduğunu söyledi; sonradan eklemek her prefab'a dokunmak demek.

Zombi prefab'ını sahneye bağlamak geliştiriciye kalacak — `Player.prefab`'da yaptığımız
gibi adım adım anlatılmalı.

## Geliştiriciyle çalışma biçimi

- **Unity'yi ilk kez kullanıyor.** Editör adımları tıklama düzeyinde anlatılmalı:
  hangi menü, hangi düğme, hangi pencere. "Prefab yap" gibi kısaltmalar kafa karıştırdı.
- **Türkçe.**
- Elle yapılacak mekanik iş yerine **araç yazmak** çok daha iyi işledi. Harita elle
  kurulacaktı; üreteç + Inspector ayar paneli yazınca iş açıldı. Aynı yaklaşım
  ölçüm protokolünde de işe yaradı (elle dört ölçüm başarısız oldu, F5 taraması çalıştı).
- Unity Editor açıkken **headless derleme yapılamıyor** (proje kilidi). Editor açıksa
  `.claude/tools/unity-log.ps1 -Errors` ile Console kontrol edilir.

## Bu oturumda kurulan kararlar

Tamamı `docs/DECISIONS.md`'de. En çok etkileyenler:

- **ADR-0004** Mirror seçildi, ADR-0001 (FishNet) supersede edildi. Gerekçe: bu oyunda
  prediction atlanabilir (co-op'ta client-authoritative hareket), lag compensation
  atlanamaz — Mirror ikincisini ücretsiz veriyor.
- **Sıralama değişti:** önce solo çekirdek döngü, sonra netcode. M-00 küçültüldü,
  netcode doğrulaması M-02'ye taşındı. İki korkuluk zorunlu kılındı.
- **Puan iki ayrı sayı:** harcanabilir bakiye + kazanılan toplam. Tek sayı olsaydı kapı
  açan oyuncu skor kaybederdi.
- **Ödül sistemi:** yarış yok, tanıma var. Kıyaslanamaz unvanlar, meta para kart havuzu
  açar (güç vermez).

## Açık sorular

1. **Harita ölçüleri oturdu mu?** Son hâli 30×16 m, 4 m tavan, 3 iç bölme. Geliştirici
   koşup geri bildirim verecekti.
2. **Sıradaki iş hangisi:** zombi mi, haritayı bitirmek mi, config importer mı?
   Öneri zombiydi — ÇK-17'ye giden en kısa yol kovalayan zombi + öldüren silah.
3. Config importer ne zaman? Tetikleyici tanımlı: 3. config dosyası ya da ilk denge turu.

## Nereye bakılır

| Ne | Nerede |
|---|---|
| Proje özeti | `docs/CONTEXT.md` ← **önce burası** |
| Kararlar | `docs/DECISIONS.md` |
| Mimari | `docs/architecture/ARCHITECTURE.md`, `adr/` |
| Performans | `docs/architecture/PERF-BUDGET.md` |
| Milestone | `design/milestones/M-00.md`, `M-01.md`, `M-02.md` |
| Sistemler | `design/systems/SYS-01/02/03`, `design/ux/draft-ekrani.md` |
| Harita | `design/levels/LVL-01-greybox.md` |
| Unity rehberi | `docs/guides/unity-baslangic.md` |
| Sahne kurulumu | `docs/guides/M0-02-sahne-kurulumu.md` |
