# Shader

[← RiseOn.Wipe2D](../../README.md)

| Shader | Dùng cho |
|---|---|
| `WipeSprite.shader` | Material của `WipeTargetSprite`. Viết cho URP 2D (tag `UniversalPipeline`, include thư viện 2D của URP) |
| `WipeImage.shader` | Material của `WipeTargetImage`, dựa trên shader UI mặc định |
| `WipeStamp.shader` | Đóng dấu lên mask, chỉ được vẽ qua CommandBuffer |

Target giữ tham chiếu tới shader của nó (shader target và `WipeStamp`) trong hai
field serialize, được *SetupEditor* điền sẵn khi thêm component trong Editor. Chính
tham chiếu đó đưa shader vào bản build, nên không cần thêm vào *Always Included
Shaders*. Thêm target bằng `AddComponent` lúc chạy thì hai field này trống: dùng
prefab có sẵn target, hoặc cho các shader vào *Always Included Shaders*.

Không được gán `WipeStamp.shader` cho bất kỳ renderer nào trong scene: nó không có
tag pipeline và không có nghĩa ở đó.

Những điều dưới đây không suy ra được từ việc đọc từng file.

## Mỗi đoạn nét là một quad capsule, không phải blit cả tấm

Giữa hai frame, cọ đi từ A tới B. Mỗi `Stroke(A, B)` vẽ **một** quad bao đúng capsule
của đoạn, fragment tính khoảng cách tới đoạn thẳng để ra độ phủ. Một draw cho cả nét
dù nhanh cỡ nào, nét liền tuyệt đối, không có spacing hay trần số dấu. Điểm đầu tiên
của một nét stroke lên chính nó, tức capsule dài 0, nên không cần hàm chấm điểm
riêng.

Tham số đổi theo từng draw (`_Segment`, `_Radius`, `_Hardness`) đi qua
`MaterialPropertyBlock`, không set lên material: giá trị material được đọc lúc buffer
**thực thi**, nên nếu có ngày gom nhiều draw vào một buffer thì mọi draw sẽ thấy giá
trị cuối cùng.

## Projection theo quy ước Unity

Quad đặt trong không gian UV của mask, với view identity và projection
`Matrix4x4.Ortho(0,1,0,1,-1,1)` **theo quy ước Unity**.
`CommandBuffer.SetViewProjectionMatrices` tự đổi sang quy ước của API đồ hoạ; nếu áp
`GL.GetGPUProjectionMatrix(..., true)` trước thì Y bị lật hai lần trên D3D và mọi dấu
cọ rơi vào vị trí đối xứng qua giữa sprite. Đã dính đúng lỗi này, phát hiện bằng test
render qua camera.

Init cũng đi qua đúng quad và projection này thay vì `Blit`, để ba đường init, erase,
reveal cùng một hệ toạ độ **theo cấu trúc**: đổi projection ở một chỗ là cả ba đổi
theo.

## Erase và Reveal dùng blend của phần cứng

Erase và Reveal không đọc mask trong shader; chúng dùng `BlendOp Min` và
`BlendOp Max`. Nhờ vậy không cần RT tạm và không có ping-pong, và chi phí một dấu chỉ
tỉ lệ với diện tích cọ.

## Target lấy mẫu mask ở LOD 0

Mip của mask chỉ để readback và sinh lười. Vì thế cả hai shader target lấy mẫu mask
ở **LOD 0** (`SAMPLE_TEXTURE2D_LOD`, `tex2Dlod`), không dùng sampler thường: sprite bị
thu nhỏ trên màn hình sẽ khiến sampler chọn mip thô, mà mip thô là bản cũ, nhìn như
dấu cọ không ăn.

## Pass fade

Fade khi chạm ngưỡng là pass 3 của `WipeStamp`, dùng **alpha blend thường**:
`dst = lerp(dst, to × spriteAlpha, delta)`. Không đọc mask trong shader, không cần RT
tạm, và vẫn giữ luật mask ≤ alpha của sprite vì đích đã nhân alpha.

`delta` mỗi frame là `(t − t₀) / (1 − t₀)`, **không phải** `dt / duration`: chuỗi
lerp như vậy chạm đúng đích tại `t = 1` bất kể frame rate, và frame cuối có
`delta = 1` nên mask bằng đích chính xác.
