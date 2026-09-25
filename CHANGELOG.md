# Lịch sử thay đổi

Mọi thay đổi đáng kể của `com.riseon.wipe2d` được ghi ở đây. Định dạng theo
[Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), đánh số theo
[Semantic Versioning](https://semver.org/lang/vi/).

## [1.0.2] - 2026-09-25

### Sửa

- Pack tự dùng Odin nên có assembly `RiseOn.Wipe2D.Requirements` của riêng nó: thiếu Odin Inspector,
  hoặc thiếu define `ODIN_INSPECTOR` ở nền tảng đang chọn, thì báo một lỗi nói rõ pack này cần Odin.

## [1.0.1] - 2026-09-25

### Sửa

- Các assembly dùng Odin có thêm `defineConstraints: ODIN_INSPECTOR`: thiếu Odin thì chúng
  được bỏ qua, và lỗi giải thích nằm ở `RiseOn.Utils.Requirements`.
- Phụ thuộc tối thiểu `com.riseon.utils` 1.0.1.
- Phụ thuộc tối thiểu `com.riseon.serializables` 1.0.1.

## [1.0.0] - 2026-09-23

### Thêm

- `WipeTargetSprite`, `WipeTargetImage`: target bị lau, mask trên GPU, tiến độ, ngưỡng hoàn tất, hỗ trợ `SpriteMask` / `Mask`.
- `Wiper`, `WiperDrag`: cọ Erase / Reveal, kéo bằng ngón tay.
- `WipeTargetProviderRef`: chọn target cho wiper.
