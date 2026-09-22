# Wiper

[← RiseOn.Wipe2D](../../README.md)

Phía cây cọ.

| Lớp | Việc |
|---|---|
| `Wiper` | Cọ: chế độ (`WipeMode.Erase` / `WipeMode.Reveal`), bán kính, độ cứng. Nhận `Move(world)` / `EndMove()` qua `IWiper` rồi gọi `Stroke` lên các target mà provider đưa ra |
| `WiperDrag` | Kéo cọ bằng ngón tay / chuột: đổi vị trí con trỏ sang world rồi gọi `IWiper`. Chỉ biết `IWiper` |

## Cách dùng

- Đặt `Wiper` và một provider trên một GameObject riêng làm vật cầm cọ.
- Tự điều khiển cọ từ code: gọi `Move(world)` mỗi frame trong lúc kéo, `EndMove()`
  khi nhấc cọ. Giữa hai lần `Move`, cọ đi thành một đoạn liền, không đứt nét.
- Kéo bằng ngón tay: thêm `WiperDrag`.
  - Trong scene: cần một Collider2D trên vật và raycaster 2D trên camera.
  - Trong canvas: cần một Graphic bật Raycast Target.
  - Đổi cách tính vị trí thì override `PointerToWorld`.

## Không có hit test, và đó là chủ ý

`Wiper.Move` gọi `Stroke` lên **mọi** target mà provider đưa ra, không kiểm cọ có đè
lên target hay không. Nét nằm ngoài mask bị loại ngay bằng một phép so AABB trong
`WipeMask.Stroke`, rẻ hơn bất kỳ raycast hay `OverlapPoint` nào. Vì thế target không
cần collider và wiper không cần biết gì về physics.

Muốn "chỉ lau cái đang chạm" thì viết một [provider](../Provider/README.md), đừng
thêm hit test vào wiper.
