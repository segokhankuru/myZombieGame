# Handoff — 2026-09-02

Bir sonraki oturumun ilk okuyacağı özet.

---

## Nerede kaldık

**M-01'de 12 işten 2'si kapandı, M1-03 ve M1-04 oyun testi bekliyor.**

| İş | Durum |
|---|---|
| M1-01 Tur ölçekleme | ✅ 12 test |
| M1-02 Ekonomi | ✅ 18 test |
| M1-03 Gri kutu harita | 🔧 üretildi + apron eklendi, ölçü ayarı geliştiricide |
| M1-04 Zombi | 🔧 **kod, prefab, sahne, NavMesh hazır — 32 test.** Oynanmadı |
| M1-05 Zombi spawn ve havuzlama | ⬅ sıradaki adaylardan |

**69 EditMode testi yeşil.** Derleme + test Unity açmadan koşuyor.

## M1-04'te ne yapıldı

- `ZombieBrain` (saf C#): belirme → pencereye yürüme → tırmanma → kovalama →
  **telegraf** → vuruş → açıklık → sıkışma kurtarması → ölüm. Sahnesiz test edilir.
- `ZombieAgent` (`Bunker.AI`): NavMesh sürüşü, zaman dilimli düşünme (8 Hz, fazları
  kaydırılmış), pencere tırmanışı, sıkışma kurtarması, animatör uzaklık kesmesi,
  gri kutuda **durumu renkle** gösterme.
- `HealthPool` + `IDamageable`: ölüm bir kez olur (ekonomi çift puan yazmasın).
- Kurulum **araç**: `Bunker/Zombi/Test Alanini Kur` prefab'ı üretir, oyuncuyu hedef
  olarak işaretler, tezgâhı sahneye koyar ve NavMesh'i bake eder. Zaten çalıştırıldı.
- Gri kutuya **apron** eklendi (bina çevresinde 6 m yürünecek şerit) — zombiler
  pencereye dışarıdan yürüyor.

## Sıradaki iş — oynamak

Unity'yi aç, `M0-Sandbox` sahnesinde **Play**'e bas. Kurulum gerekmiyor.

```
F6 dogum ac/kapa    F7/F8 tur -/+    F9 hepsini oldur    sol tik hata ayiklama atisi
```

Bakılacak sorular, sırayla:

1. Zombi pencereden girerken **okunabiliyor mu** — geldiğini görüyor musun?
2. Telegraf (turuncuya dönme) yeterli mi, yoksa vuruş habersiz mi geliyor?
3. Tur 5–8 civarında zombiler ne zaman ürkütücü olmaya başlıyor?
4. Sıkışan zombi var mı (mavi renk = sıkışma), nerede?

Bulguları `config/balance/zombie.json` içinde ayarla — kod değişmeden. Her sayının
açıklaması `config/schema/zombie.schema.json` içinde, "dışına çıkarsan oyuncu ne
hisseder" cümlesiyle birlikte.

## Açık kararlar

1. **Sıradaki iş hangisi:** M1-05 (spawn + ağ seam'i) mi, config importer mı?
   Importer'ın tetikleyicisi **karşılandı** — üçüncü config dosyası eklendi. Sayılar üç
   dosyada iki yerde duruyor; ne kadar beklerse sapma o kadar büyür.
2. Ceset davranışı: şu an zombi anında yok oluyor. Havuzlama M1-05'te, ceset/ragdoll
   kararı sanat aşamasında.
3. Harita ölçüleri hâlâ açık (M1-03) — zombilerle koşunca cevabı netleşir.

## Geliştiriciyle çalışma biçimi

- **Unity'yi ilk kez kullanıyor, Türkçe.** Editör adımı istemiyor: "şunu tıkla" yerine
  **araç yaz ve kendin çalıştır**. Bu oturumda kurulumun tamamı `-executeMethod` ile
  başsız koştu, geliştiricinin tek yapacağı Play'e basmak.
- Unity Editor açıkken başsız derleme yapılamaz (proje kilidi). Açıksa
  `.claude/tools/unity-log.ps1 -Errors`.

## Yeni araçlar

| Araç | Ne yapar |
|---|---|
| `.claude/tools/unity-test.ps1` | Derler + EditMode testlerini koşar, kısa özet döner |
| `.claude/tools/unity-exec.ps1` | Herhangi bir editör metodunu başsız çalıştırır |

## Nereye bakılır

| Ne | Nerede |
|---|---|
| Proje özeti | `docs/CONTEXT.md` ← **önce burası** |
| Kararlar | `docs/DECISIONS.md` |
| Zombi kararı | `Assets/_Project/Code/Systems/Ai/ZombieBrain.cs` |
| Zombi motoru | `Assets/_Project/Code/AI/ZombieAgent.cs` |
| Zombi ayarları | `config/balance/zombie.json` + `config/schema/zombie.schema.json` |
| Kurulum aracı | `Assets/_Project/Code/Editor/ZombieSetup.cs` |
| Milestone | `design/milestones/M-01.md` |
| Harita | `design/levels/LVL-01-greybox.md` |
