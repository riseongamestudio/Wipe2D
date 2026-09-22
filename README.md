# RiseOn.Wipe2D

Lau sprite bằng cọ: `WipeMode.Erase` xoá tới đâu mất tới đó, `WipeMode.Reveal` lau
tới đâu sprite về nguyên bản tới đó. Không vẽ màu. Chạy cho SpriteRenderer trong
scene và cho Image trong canvas, cùng một wiper, có đo tiến độ và tự hoàn tất khi
gần xong.

Package `com.riseon.wipe2d`, namespace `RiseOn.Wipe2D`.

## Mục lục

- [Yêu cầu](#yêu-cầu)
- [Cài đặt](#cài-đặt)
- [Tổng quan](#tổng-quan)
- [Hướng dẫn nhanh](#hướng-dẫn-nhanh)
- [Mở rộng](#mở-rộng)
- [Thành phần](#thành-phần)
- [Lịch sử thay đổi](#lịch-sử-thay-đổi)
- [Giấy phép](#giấy-phép)

## Yêu cầu

| Phụ thuộc | Cách có | Dùng cho |
|---|---|---|
| Unity 6000.3 | | Bản đang dùng để phát triển |
| [`com.riseon.utils`](https://github.com/riseongamestudio/Utils/tree/main/Core#readme) 1.0.0 | Tự cài theo `package.json` | Lớp nền, Undo |
| [`com.riseon.serializables`](https://github.com/riseongamestudio/Serializables#readme) 1.0.0 | Tự cài theo `package.json` | Tham chiếu target qua interface |
| `com.unity.ugui` 2.0.0 | Tự cài theo `package.json` | `WipeTargetImage`, `WiperDrag` trong canvas |
| `com.unity.render-pipelines.universal` 17.3.0 | Tự cài theo `package.json` | Shader của `WipeTargetSprite` (URP 2D) |
| [Odin Inspector](https://odininspector.com) | Cài tay từ Asset Store | Inspector của các component |
| [DOTween](https://dotween.demigiant.com) | Cài tay từ Asset Store | `com.riseon.utils` cần |

"Tự cài" là khi cài qua OpenUPM; cài bằng git URL thì phải cài các package RiseOn
kia trước. Odin và DOTween không có trên UPM nên phải cài vào project trước.

## Cài đặt

**OpenUPM** (khuyên dùng): thêm registry OpenUPM với scope `com.riseon` vào
`Packages/manifest.json`, rồi thêm package:

```json
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": ["com.riseon"]
    }
  ],
  "dependencies": {
    "com.riseon.wipe2d": "1.0.0"
  }
}
```

**Git URL**: cài `com.riseon.utils` và `com.riseon.serializables` trước, rồi
*Package Manager → + → Add package from git URL*:

```
https://github.com/riseongamestudio/Wipe2D.git#v1.0.0
```

**Thư mục local**: `"com.riseon.wipe2d": "file:D:/path/to/Wipe2D"`.

## Tổng quan

| Phần | Việc |
|---|---|
| **Target** (`WipeTargetSprite`, `WipeTargetImage`) | Thứ bị lau. Giữ một mask trên GPU, đo tiến độ `Progress` từ 0 tới 1 |
| **Wiper** (`Wiper`, `WiperDrag`) | Cây cọ: chế độ, bán kính, độ cứng; `WiperDrag` cho kéo bằng ngón tay |
| **Provider** (`WipeTargetProviderRef`) | Quyết định wiper lau những target nào |

Code bên ngoài chỉ chạm ba interface: `IWipeTarget` (`Progress`,
`OnProgressChanged`, `OnProgressThresholdReached`, `Stroke`), `IWiper`
(`Move(world)`, `EndMove()`), và `IWipeTargetProvider` khi cần cách chọn target
khác. Mọi thứ còn lại là `internal`.

## Hướng dẫn nhanh

1. Trên sprite cần lau: thêm `WipeTargetSprite` (SpriteRenderer) hoặc
   `WipeTargetImage` (Image, Type = Simple, không Preserve Aspect). Không cần
   collider, không cần bật Read/Write cho texture.
2. Chọn *Init State*: `Visible` nếu sẽ bị xoá, `Hidden` nếu sẽ được lộ ra.
3. Trên vật cầm cọ (một GameObject riêng): thêm `Wiper` và
   `WipeTargetProviderRef`, kéo các target vào provider.
4. Muốn kéo bằng ngón tay thì thêm `WiperDrag`. Trong scene nó cần một Collider2D
   và raycaster 2D trên camera; trong canvas nó cần một Graphic bật Raycast Target.
5. Nghe tiến độ. `OnProgressThresholdReached` là `UnityEvent`, gắn được cả trong
   Inspector:

   ```csharp
   target.OnProgressChanged += p => progressBar.fillAmount = p;
   target.OnProgressThresholdReached.AddListener(() => Debug.Log("Xong"));
   ```

Mọi component có nút *SetupEditor* và tự chạy nó lúc `Reset`, nên kéo component
vào là các ô tham chiếu tự điền. Sprite bị một mask che bớt thì kéo cái mask đó vào
nhóm *Mask* của target (xem [WipeTarget](Runtime/WipeTarget/README.md#mask)).

## Mở rộng

Không lớp public nào `sealed`. Ba đường:

- **Renderer kiểu khác**: kế thừa `WipeTarget` ([chi tiết](Runtime/WipeTarget/README.md#mở-rộng)).
- **Cách chọn target khác**: kế thừa `WipeTargetProvider` ([chi tiết](Runtime/Provider/README.md)).
- **Chỉnh một lớp có sẵn**: các điểm móc để `virtual` gồm `Wiper.Move` / `EndMove` /
  `OnDisable`, `WiperDrag.PointerToWorld`, `WipeTarget.Clear` / `Stroke` / `Awake` /
  `OnDestroy` / `LateUpdate`, và `SetupEditor` ở mọi component.

## Thành phần

| Thành phần | Việc | Chi tiết |
|---|---|---|
| Target | Mask, tiến độ, ngưỡng hoàn tất, mask che, giới hạn về sprite | [Runtime/WipeTarget](Runtime/WipeTarget/README.md) |
| Wiper | Cọ, kéo bằng ngón tay, vì sao không có hit test | [Runtime/Wiper](Runtime/Wiper/README.md) |
| Provider | Chọn target cho wiper | [Runtime/Provider](Runtime/Provider/README.md) |
| Shader | Shader của target và cách đóng dấu trên GPU | [Runtime/Shaders](Runtime/Shaders/README.md) |

## Lịch sử thay đổi

Xem [CHANGELOG.md](CHANGELOG.md).

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).
