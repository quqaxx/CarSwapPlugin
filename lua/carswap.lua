-- Araç değiştirme menüsü (istemci). Sunucu plugini (CarSwapPlugin) ile konuşur.
-- Menüyü açmak için oyun ayarlarında "carswap/Open menu" tuşuna bir tuş ata.

local config = ac.configValues({ models = "" })

local models = {}
for m in string.gmatch(config.models, "[^;]+") do
  models[#models + 1] = m
end

local slots = {}          -- slots[i] = { used = n, total = n }  (i = 1-based model index)
local gotSlots = false
local clock = 0
local lastHello = -10
local menuOpen = false
local waiting = false     -- onay bekliyor / yeniden bağlanıyor
local statusText = ""
local statusUntil = 0

local denyTexts = {
  [1] = "Geçersiz araç.",
  [2] = "Zaten bu araçtasın.",
  [3] = "Bu aracın boş slotu kalmadı.",
  [4] = "Biraz bekle, çok hızlı değiştiriyorsun.",
}

local function setStatus(text, seconds)
  statusText = text
  statusUntil = clock + (seconds or 4)
end

-- ayrı bir event'te struct alanları sunucudaki paketle birebir aynı olmalı
local helloEvent = ac.OnlineEvent({
  ac.StructItem.key("CarSwap_Hello"),
  dummy = ac.StructItem.byte(),
})

local requestEvent = ac.OnlineEvent({
  ac.StructItem.key("CarSwap_Request"),
  modelIndex = ac.StructItem.byte(),
  position = ac.StructItem.vec3(),
  direction = ac.StructItem.vec3(),
})

local pendingRestore = nil  -- { pos = vec3, dir = vec3 } yeni araca geçince uygulanır

ac.OnlineEvent({
  ac.StructItem.key("CarSwap_Restore"),
  position = ac.StructItem.vec3(),
  direction = ac.StructItem.vec3(),
}, function(sender, message)
  if sender ~= nil then return end
  pendingRestore = { pos = vec3(message.position.x, message.position.y, message.position.z),
                     dir = vec3(message.direction.x, message.direction.y, message.direction.z) }
end)

ac.OnlineEvent({
  ac.StructItem.key("CarSwap_Slots"),
  modelIndex = ac.StructItem.byte(),
  used = ac.StructItem.byte(),
  total = ac.StructItem.byte(),
}, function(sender, message)
  if sender ~= nil then return end
  slots[message.modelIndex + 1] = { used = message.used, total = message.total }
  gotSlots = true
end)

ac.OnlineEvent({
  ac.StructItem.key("CarSwap_Go"),
  modelIndex = ac.StructItem.byte(),
  delay = ac.StructItem.uint16(),
}, function(sender, message)
  if sender ~= nil then return end
  local model = models[message.modelIndex + 1]
  if not model then waiting = false return end

  setStatus("Araç değiştiriliyor...", 10)
  setTimeout(function()
    ac.reconnectTo({ carID = model })
  end, message.delay)
end)

ac.OnlineEvent({
  ac.StructItem.key("CarSwap_Deny"),
  reason = ac.StructItem.byte(),
}, function(sender, message)
  if sender ~= nil then return end
  waiting = false
  setStatus(denyTexts[message.reason] or "İstek reddedildi.")
end)

local openButton = ac.ControlButton("carswap/Open menu")

function script.update(dt)
  clock = clock + dt

  if openButton:pressed() then menuOpen = not menuOpen end

  -- yeni araca geçildi, eski konuma ışınla (fizik hazır olunca)
  if pendingRestore ~= nil then
    local c = ac.getCar(0)
    if c ~= nil and c.physicsAvailable then
      physics.setCarPosition(0, pendingRestore.pos, pendingRestore.dir)
      pendingRestore = nil
    end
  end

  -- slot durumu gelene kadar sunucudan iste
  if not gotSlots and clock - lastHello > 3 then
    lastHello = clock
    helloEvent({ dummy = 0 })
  end
end

function script.drawUI()
  if not menuOpen then return end

  local winSize = ac.getUI().windowSize
  local size = vec2(360, 80 + #models * 30)
  local pos = vec2(winSize.x - size.x - 20, 120)

  ui.toolWindow("carswap_menu", pos, size, function()
    ui.text("ARAÇ DEĞİŞTİR")
    ui.separator()

    local current = ac.getCarID(0)
    for i, model in ipairs(models) do
      local s = slots[i]
      local used, total = 0, 0
      if s then used, total = s.used, s.total end

      local full = total > 0 and used >= total
      local label = string.format("%s    %d/%d  %s", model, used, total, full and "DOLU" or "BOŞTA")
      local disabled = waiting or full or model == current or not gotSlots

      if ui.button(label, vec2(-0.1, 0), disabled and ui.ButtonFlags.Disabled or ui.ButtonFlags.None) then
        waiting = true
        setStatus("İstek gönderildi...", 10)
        local c = ac.getCar(0)
        local look = vec3(c.look.x, 0, c.look.z)
        if look:length() < 0.001 then look = vec3(1, 0, 0) else look = look:normalize() end
        requestEvent({
          modelIndex = i - 1,
          position = vec3(c.position.x, c.position.y, c.position.z),
          direction = look,
        })
      end
    end

    if clock < statusUntil then
      ui.separator()
      ui.text(statusText)
    end
  end)
end
