# Provider

[← RiseOn.Wipe2D](../../README.md)

Provider quyết định một wiper lau những target nào. Tách riêng để thêm cách chọn
mới (theo id, tất cả target trong scene, chỉ cái đang chạm...) mà không đụng
`Wiper`.

| Lớp | Việc |
|---|---|
| `IWipeTargetProvider` | Hợp đồng mà `Wiper` dùng |
| `WipeTargetProvider` | Base để kế thừa |
| `WipeTargetProviderRef` | Danh sách target kéo thả trong Inspector |

## Cách dùng

Thêm `WipeTargetProviderRef` cạnh `Wiper`, kéo các target vào. Target nào có trong
danh sách mới bị cọ này lau.

## Viết provider khác

Kế thừa `WipeTargetProvider` và trả về danh sách target của mình. `Wiper` không kiểm
cọ có đè lên target hay không ([lý do](../Wiper/README.md#không-có-hit-test-và-đó-là-chủ-ý)),
nên muốn giới hạn target nào bị lau thì lọc ngay trong provider.
