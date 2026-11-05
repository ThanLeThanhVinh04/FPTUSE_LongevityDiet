# Kịch bản Demo Chung kết PRN232 (7/7 Mục theo Đề cương) & Kế hoạch Khắc phục Sự cố

Tài liệu này là hướng dẫn chính thức dành cho nhóm thực hiện buổi thuyết trình / bảo vệ đồ án cuối kỳ **PRN232** cho đồ án **Longevity Diet Companion**.

Kịch bản được thiết kế bám sát **7 tiêu chí bắt buộc** trong PDF đánh giá của giảng viên, với thời lượng tối ưu từ **10 đến 12 phút**.

---

## 1. Bảng phân công & Chuẩn bị trước Demo

| Thứ tự | Nội dung trình bày | Trọng số Rubric | Người phụ trách đề xuất |
|---|---|---|---|
| **Mục 1** | Kiến trúc hệ thống (System Architecture & C4) | 20% | Thành viên 1 / 4 |
| **Mục 2** | REST API & Chuẩn bảo mật (JWT, CRUD, ProblemDetails) | 20% | Thành viên 2 |
| **Mục 3** | Background Job Execution (Worker & Outbox) | 10% | Thành viên 1 |
| **Mục 4** | Message Broker Integration (Redis Streams & Consumer Group) | 15% | Thành viên 2 |
| **Mục 5** | gRPC Communication (Độc lập Recommendation Service) | 15% | Thành viên 3 |
| **Mục 6** | Docker Deployment & Volume Persistence | 10% | Thành viên 2 |
| **Mục 7** | End-to-End Business Flow & Controlled Failure Recovery | 10% | Thành viên 3 |
| **Tổng** | **Đầy đủ 7/7 Mục Rubric** | **100%** | **Cả nhóm** |

---

## 2. Kịch bản chi tiết 7 Bước Demo (10-12 phút)

### Bước 1: Giới thiệu Kiến trúc Hệ thống (20% điểm) — 1.5 phút
- **Thao tác**: Mở sơ đồ kiến trúc (C0 Context, C1 Containers).
- **Lời thoại mẫu**:
  > *"Kính thưa Thầy/Cô, hệ thống Longevity Diet Companion được thiết kế theo mô hình Microservices/Distributed Systems chuẩn .NET 9. Chúng em áp dụng kiến trúc phân tầng Clean Architecture (API -> Services -> Repositories -> SQL Server EF Core). Trình duyệt React Web chỉ giao tiếp duy nhất qua REST API (Port 8080/5110). REST API giao tiếp với Recommendation Engine thông qua gRPC độc lập qua giao thức HTTP/2 (Port 8081/5010). Các tác vụ bất đồng bộ được xử lý thông qua Transactional Outbox đẩy vào Redis Streams và Worker độc lập tiêu thụ theo Consumer Group."*

### Bước 2: Trình diễn REST API & Semantic Status (20% điểm) — 2 phút
- **Thao tác**: Mở giao diện Swagger UI (`http://localhost:5110/swagger` hoặc `http://localhost:8080/swagger`).
- **Điểm nhấn chứng minh**:
  1. **JWT Authentication**: Thực hiện `POST /api/v1/auth/register` và `POST /api/v1/auth/login`. Chỉ rõ Token trả về cùng Cookie an toàn `ldc_refresh`.
  2. **CRUD & Search/Filter/Sort/Pagination**: Trình diễn `GET /api/v1/meal-plans/candidates` với các tham số phân trang, tìm kiếm món ăn theo từ khóa, lọc theo nhóm món (`Bowl`, `RiceBowl`).
  3. **REST Semantics & RFC 7807**: Cố tình gửi yêu cầu không hợp lệ (ví dụ: ngày sinh không hợp lệ hoặc thiếu dữ liệu) -> Chỉ rõ API trả về `ProblemDetails` chuẩn RFC 7807 với HTTP 400 Bad Request.

### Bước 3: Trình diễn Background Job Execution (10% điểm) — 1.5 phút
- **Thao tác**: Mở Terminal chạy `LongevityDiet.Worker`.
- **Điểm nhấn chứng minh**:
  1. Chỉ rõ Worker kế thừa `BackgroundService` chạy trên tiến trình riêng biệt, hoàn toàn tách biệt khỏi HTTP request thread của REST API.
  2. Khi có sự kiện kinh doanh phát sinh, Worker quét bảng `OutboxMessages` theo chu kỳ để đảm bảo tính tin cậy tuyệt đối (Reliable Delivery).
  3. Khi hoàn tất, trạng thái Outbox được cập nhật `ProcessedAt`, đảm bảo không mất dữ liệu ngay cả khi hệ thống sập giữa chừng.

### Bước 4: Trình diễn Message Broker Redis Streams (15% điểm) — 1.5 phút
- **Thao tác**: Mở Terminal và kết nối Redis CLI (`redis-cli` trong docker container):
  ```bash
  docker exec -it longevity-diet-redis-1 redis-cli
  XINFO STREAM ldc:events:stream
  XINFO GROUPS ldc:events:stream
  ```
- **Điểm nhấn chứng minh**:
  1. **Producer**: API ghi Outbox và Worker xuất bản sự kiện vào Stream bằng lệnh `XADD`.
  2. **Consumer Group**: Worker đọc theo nhóm bằng `XREADGROUP GROUP ldc-worker-group`.
  3. **XACK & Idempotency**: Sau khi xử lý xong, sự kiện được xác nhận bằng `XACK` và lưu vết vào bảng `ProcessedEvents` trong SQL Server để chống trùng lặp (Idempotent Consumer).

### Bước 5: Trình diễn gRPC Communication độc lập (15% điểm) — 1.5 phút
- **Thao tác**: Mở file `src/LongevityDiet.Recommendation.Grpc/Protos/recommendation.proto`.
- **Điểm nhấn chứng minh**:
  1. Service `LongevityDiet.Recommendation.Grpc` chạy trên process và port riêng (`http://localhost:5010` hoặc `8081`).
  2. Giao thức nhị phân Protobuf chạy trên **HTTP/2**.
  3. Mở log của API và gRPC host: Khi người dùng bấm *"Gợi ý & Đổi món v2"*, REST API đóng vai trò gRPC Client gọi RPC `RankMeals(RankMealsProtoRequest)`.
  4. Thuật toán xếp hạng đa tiêu chí (Five Pillars) tính điểm chuẩn xác, tự động loại trừ dị ứng cứng và trừ điểm đối với các món ăn nằm trong danh sách không thích (Soft Dislike).

### Bước 6: Trình diễn Docker Deployment & Volume Persistence (10% điểm) — 1.5 phút
- **Thao tác**: Mở PowerShell chạy:
  ```powershell
  docker compose ps
  docker volume ls
  ```
- **Điểm nhấn chứng minh**:
  1. Toàn bộ hệ thống chạy đồng bộ trên Docker Compose qua mạng nội bộ `ldc-network`.
  2. Các container có cấu hình **Healthcheck** tự động giám sát trạng thái `healthy`.
  3. **Volume Persistence**: Dữ liệu SQL Server được mount ra volume `sql-data` và Redis được mount ra `redis-data` (bật AOF persistence). Dữ liệu người dùng hoàn toàn nguyên vẹn kể cả khi restart container.

### Bước 7: End-to-End Business Flow & Khắc phục Sự cố (10% điểm) — 2.5 phút
- **Thao tác**: Mở trình duyệt tại `http://localhost:5173/app`:
  1. **Lập thực đơn 7 ngày**: Chọn chế độ Chủ đạo thực vật, dị ứng đậu phộng/hải sản -> Tạo kế hoạch -> Kế hoạch 7 ngày hiển thị chuẩn dinh dưỡng trường thọ Valter Longo.
  2. **Đổi món v2**: Chọn một món ăn bất kỳ -> Bấm Đổi món -> Hệ thống gọi gRPC và cập nhật món thay thế nguyên tử.
  3. **Nhật ký & Khung giờ 12h**: Ghi nhận bữa sáng (07:30) và bữa tối (18:30) -> Hệ thống tự động tính khung giờ ăn 11 tiếng (< 12 tiếng) và cách giờ ngủ 4.5 tiếng (> 3 tiếng) đạt chuẩn nhịp sinh học.
  4. **FMD Safety Gate & Theo dõi chu kỳ**:
     - Thử chọn câu hỏi rủi ro (tiểu đường / mang thai) -> Hệ thống lập tức chặn với mã cảnh báo đỏ.
     - Chọn an toàn -> Kích hoạt chu kỳ 5 ngày -> Ghi nhận nhật ký Ngày 1 (1100 kcal) -> Thử bấm nút khẩn cấp *"Dừng chu kỳ ngay"* -> Chuyển vào Bảng lịch sử chu kỳ FMD an toàn.

---

## 3. Kịch bản Kiểm tra Sự cố Có kiểm soát (Controlled Failure & Recovery)

Giảng viên rất đánh giá cao khi sinh viên chứng minh được tính sẵn sàng cao và khả năng phục hồi (Resilience) của hệ thống:

### Kịch bản A: Giả lập sự cố mất kết nối gRPC
1. **Hành động**: Tạm dừng container gRPC:
   ```powershell
   docker compose stop recommendation-grpc
   ```
2. **Hiện tượng**: Trên Web UI, người dùng bấm đổi món ăn.
3. **Kết quả**: REST API không bị crash (500), mà trả về thông báo lỗi thân thiện chuẩn RFC 7807: `503 Service Unavailable - Dịch vụ gợi ý gRPC tạm thời gián đoạn, kế hoạch chính vẫn an toàn`.
4. **Phục hồi**: Bật lại container:
   ```powershell
   docker compose start recommendation-grpc
   ```
   Thao tác đổi món lập tức hoạt động lại bình thường.

### Kịch bản B: Giả lập sự cố ngắt kết nối Worker / Redis
1. **Hành động**: Tạm dừng container Redis:
   ```powershell
   docker compose stop redis
   ```
2. **Hiện tượng**: Người dùng tạo thực đơn trên Web UI.
3. **Kết quả**: REST API vẫn cam kết dữ liệu kế hoạch và lưu sự kiện vào SQL Transactional Outbox an toàn (không bị mất mát giao dịch).
4. **Phục hồi**: Bật lại Redis:
   ```powershell
   docker compose start redis
   ```
   Worker tự động quét các bản ghi Outbox chưa xử lý và tiếp tục đẩy vào Redis Streams, hoàn tất toàn bộ chu trình bất đồng bộ mà không cần can thiệp thủ công.

