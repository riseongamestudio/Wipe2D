# WipeTarget

[← RiseOn.Wipe2D](../../README.md)

Thứ bị lau. `WipeTarget` là phần chung: giữ mask, đổi toạ độ world sang pixel qua
các hook trừu tượng, đổi tỉ lệ nhìn thấy thành `Progress`.
`Concretes/WipeTargetSprite` và `Concretes/WipeTargetImage` chỉ điền các hook đó.
`WipeMask` (internal) là mask R8: đóng dấu capsule, đọc lại tỉ lệ còn nhìn thấy; nó
làm việc bằng pixel của sprite, không biết renderer là gì.

- [Tiến độ](#tiến-độ)
- [Ngưỡng hoàn tất](#ngưỡng-hoàn-tất)
- [Mask](#mask)
- [Giả định về sprite và transform](#giả-định-về-sprite-và-transform)
- [Material](#material)
- [Mở rộng](#mở-rộng)
- [Bên trong: mask trên GPU](#bên-trong-mask-trên-gpu)

## Tiến độ

`Progress` là 0 ở trạng thái ban đầu và 1 khi đã lau hết theo chiều ngược lại. Nó
suy từ *Init State*, nên target bị xoá lẫn target được lộ ra đều đọc 0 → 1 mà không
cần biết wiper đang ở chế độ nào. Cập nhật bất đồng bộ qua `OnProgressChanged`, trễ
vài frame: đừng dùng `Progress` cho logic cần tức thời trong cùng frame.

## Ngưỡng hoàn tất

Nhóm *Threshold*: `Progress` vượt *Progress Threshold* thì bắn
`OnProgressThresholdReached` (một `UnityEvent`, gắn được trong Inspector), và phần
còn lại tự fade về trạng thái ngược với *Init State* trong *Fade Duration* giây,
người chơi không phải chà nốt mấy pixel cuối.

- Ngưỡng được kiểm trong callback readback, không phải mỗi frame, và chỉ bắn một
  lần.
- Fade chạm đúng đích ở cuối, bất kể frame rate; lúc đó `Progress` được đặt thẳng
  về 1, không lơ lửng ở 0.99.
- Từ lúc bắn ngưỡng, `Stroke` bị bỏ qua, không wiper nào kéo ngược được fade.
- Fade xong thì component tự tắt `enabled`: hết `LateUpdate`, hết readback. Muốn
  dùng lại target thì phải bật lại và dựng lại mask; hiện chưa có đường đó.

## Mask

`SpriteMask` và `Mask` cắt bằng stencil lúc render, tầng mà wipe mask không nhìn
thấy. Nếu chỉ một phần sprite thực sự lộ ra thì tiến độ vẫn đếm cả sprite và
không bao giờ chạm 1. Nhóm *Mask* sửa việc đó: kéo thẳng component đang che vào,
`SpriteMask` cho `WipeTargetSprite`, `Mask` cho `WipeTargetImage`.

Không có tham số nào phải chỉnh tay, vì đọc hết từ component:

- **Có che hay không, trong hay ngoài**: `SpriteRenderer.maskInteraction`. Để
  `None` là bỏ qua mask kể cả khi ô đã điền. Bên canvas luôn là trong, theo uGUI.
- **Mask đang tắt**: component tắt hay GameObject không active thì không cắt, y
  như Unity.
- **Custom Range**: bật lên thì chỉ cắt khi renderer nằm giữa `Back` và `Front`, so
  sorting layer trước rồi tới order.
- **Mask Source**: `Sprite` lấy ô sprite của mask; `Supported Renderers` lấy sprite
  của SpriteRenderer cùng GameObject, kể cả `flipX` / `flipY` của renderer đó.
- **Cutoff**: `SpriteMask.alphaCutoff`; bên canvas là `0.001`, đúng ngưỡng
  `UNITY_UI_ALPHACLIP` của shader UI.
- **Hình dạng**: sprite tìm được ở trên. `Mask` bên canvas không có sprite thì cắt
  bằng rect của nó.
- **Không ảnh hưởng**: *Sprite Sort Point* chỉ đổi thứ tự vẽ của mask, nên bỏ qua.

Giới hạn:

- Ngoài rect của mask tính là clip alpha 0: bị che ở chế độ trong, lộ ở chế độ
  ngoài.
- Clip được chụp một lần lúc khởi tạo, nên target và mask phải cứng với nhau: phóng
  to cả cụm thì không sao, tách ra chạy riêng thì clip cũ.
- Một mask cho mỗi target. Unity cho nhiều SpriteMask chồng nhau, ca đó phải tự gộp.
- Renderer làm mask ở *Draw Mode* Sliced hay Tiled thì hình cắt là mesh chứ không
  còn là sprite; pack không mô phỏng.

## Giả định về sprite và transform

- Atlas được xử lý: rect của sprite được map qua `_SpriteRect` và `_MaskST`.
- `flipX` / `flipY` của SpriteRenderer được bù trong `WorldToPixel`.
- `WipeTargetImage` map cả rect lên cả sprite, nên Image phải là Simple và không
  Preserve Aspect; Sliced / Tiled / Filled dùng UV khác và sẽ sai.
- Scale không đồng nhất chỉ được xấp xỉ theo trục X khi đổi bán kính cọ.
- Mask dựng một lần trong `Awake` theo sprite lúc đó. Đổi sprite lúc chạy thì mask
  không tự khớp; hiện chưa có hàm dựng lại.
- Toạ độ là world ở mọi nơi. Trong canvas, wiper và image cùng cây nên
  `InverseTransformPoint` vẫn ra đúng local của rect ở mọi render mode.

## Material

Target gán một material instance lên renderer khi khởi tạo và trả lại material gốc
khi bị destroy. Nếu chỗ khác cũng đổi material của cùng renderer thì hai bên sẽ
giẫm nhau; hãy để target làm chủ.

## Mở rộng

Renderer kiểu khác: kế thừa `WipeTarget`, điền `TargetShaderName`, `Sprite`,
`WorldToPixel`, `WorldToPixelRadius`, `PixelToWorld`, `ApplyMaterial`, cùng
`TryGetClip` / `WorldToClipUV` cho phần mask. Không cần đụng tới `WipeMask`.

`WipeMask` là `internal sealed` và giữ nguyên: nó là lõi GPU, không nằm trong hợp
đồng với bên ngoài, và `sealed` giúp devirtualise đường `Stroke` chạy mỗi frame.

## Bên trong: mask trên GPU

Những điều không suy ra được từ việc đọc từng file. Cách đóng dấu từng nét nằm ở
[Shaders](../Shaders/README.md).

### Mask khởi tạo bằng alpha của sprite, không phải bằng 1

Mask là RenderTexture R8 phủ đúng rect của sprite. Lúc `Clear(WipeInitState.Visible)`
nó được **vẽ alpha của sprite** vào bằng một quad phủ kín mask, không phải fill
trắng. Ba thứ dựa vào đó:

- Shader target dùng `col.a = min(col.a, mask)`. Vì mask bắt đầu bằng đúng alpha nên
  vùng chưa lau hiện y như tác giả vẽ, không bị nhân alpha hai lần.
- Tỉ lệ còn nhìn thấy = trung bình mask hiện tại / trung bình mask lúc khởi tạo, rồi
  `Progress` suy từ đó theo `initState`. Vùng trong suốt quanh sprite không tính vào
  mẫu số, nên "còn nhìn thấy 1.0" nghĩa là "toàn bộ phần có hình", không phải "toàn
  bộ rect".
- Pass Reveal ghi `max(mask, coverage × spriteAlpha)`, **nhân với alpha của
  sprite**. Nếu ghi `max(mask, coverage)` thì lau ra vùng trong suốt sẽ đẩy mask vượt
  baseline và `Progress` sai.

Đổi bất kỳ vế nào trong ba vế trên thì phải đổi cả ba. Khi có mask che, luật này
thành "mask ≤ alpha × clip": `SpriteAlpha` nhân thêm clip nên init, reveal và fade
cùng tôn trọng nó, và baseline chỉ đếm vùng thấy được.

### Tiến độ đọc từ mip, và có một lần đọc đồng bộ

Sau mỗi đợt dấu, `GenerateMips()` rồi `AsyncGPUReadback` một mip nhỏ; trung bình của
nó chính là tỉ lệ mask. `readbackMipOffset` chọn mip: 0 là 1×1, mỗi bậc thêm là 4
lần texel để trung bình mịn hơn. Mip chỉ để readback, sinh lười theo
`readbackInterval`.

Baseline đo **một lần đồng bộ** lúc tạo mask bằng `WaitForCompletion()`. Chấp nhận vì
chỉ chạy lúc load và mảnh đọc rất nhỏ. Đừng chuyển nó sang bất đồng bộ mà không
nghĩ tới `initState == WipeInitState.Hidden`: lúc đó mask bị clear trước khi
readback về, baseline sẽ bằng 0.

### Clip của mask che

Lúc khởi tạo, base đẩy ba góc UV của wipe mask qua world sang UV của mask che bằng
`PixelToWorld` của mình và `WorldToClipUV` của concrete, dựng ra một ma trận affine
cho shader. Uniform ma trận mặc định là **zero** chứ không phải identity, nên
`WipeMask` luôn ghi clip kể cả khi không có mask che, qua `WipeClip.None`.
