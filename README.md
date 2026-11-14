# CarSwapPlugin (AssettoServer)

Oyundan çıkmadan entry list'teki başka bir araca geçiş. İlk sürüm, test edilmedi.

## Nasıl çalışır
1. Oyuncu menüden araç seçer (istemci Lua scripti, sunucu tarafından otomatik dağıtılır).
2. Sunucu o modelden boş bir slot bulur ve oyuncuya kısa süre ayırır (`SlotReservations` + `CarSwapSlotFilter`).
3. Sunucu `CarSwap_Go` paketi yollar, istemci `ac.reconnectTo({ carID = <model> })` ile yeniden bağlanır.
4. Doluluk (kullanılan/toplam) her saniye değişiklik varsa herkese yayınlanır.

## Kurulum
1. Bu klasörü `AssettoServer/Plugins/CarSwapPlugin` olarak kopyala.
2. `AssettoServer.slnx` içine diğer pluginlerin yanına ekle:
   `<Project Path="Plugins\CarSwapPlugin\CarSwapPlugin.csproj" />`
3. Derle (`dotnet build -c Release`), çıkan `CarSwapPlugin` klasörünü sunucunun `plugins/` klasörüne koy.
4. `extra_cfg.yml`:
   ```yaml
   EnableClientMessages: true
   EnablePlugins:
   - CarSwapPlugin
   ```
5. Oyunda ayarlar > kontroller kısmında "carswap/Open menu" tuşuna bir tuş ata.

## Bilinen belirsizlikler (test edilecek)
- `ac.reconnectTo({ carID = model })` ile model adı veriliyor. `VotingPresetPlugin` aynı fonksiyonu `ac.getCarID(0)` ile çağırıyor, buna güvenerek yazıldı ama bu kullanımı doğrulamadım.
- Yeniden bağlanmanın yükleme ekranı gösterip göstermediği ve ne kadar sürdüğü bilinmiyor.
- Adminler slot filtrelerini atlıyor, yani admin ayrılmış bir slotu kapabilir.
- Konum taşıma var: istek anındaki konum ve yön sunucuya gider, yeniden bağlanınca `physics.setCarPosition` ile (FastTravelPlugin'in kullandığı yöntem) yeni araç oraya konur. Sunucu, istemcinin yolladığı konumu kendi bildiği konumla karşılaştırır (100 m'den fazla fark varsa konum taşınmaz). Hız taşınmıyor, yeni araç duruyor.
- AssettoServer AGPL lisanslı, plugin de açık kaynak olmalı.
