welcome to my prj 

turiol use tocfl quiz :
you make 1 foulder into disk D 
or you can dowload my foulder =Đ
form this:  https://drive.google.com/file/d/1EiHt9dsL6ppyNcJfaU_5plG05jBUhl0e/view?usp=drive_link

thank you and sorry because enlis not good =Đ
this prj build by AI hehe.
chi tiết bằng tiếng việt =))
# TocflQuiz (WinForms .NET 8)

Ứng dụng Windows giúp bạn:

1) **Làm đề TOCFL (Listening/Reading)** bằng PDF/ảnh + MP3, có **lưu tiến độ** và **lịch ôn tập kiểu spaced repetition**.  
2) **Quản lý học phần (flashcards)**: nhập liệu nhanh từ Word/Excel/Google Docs, học thẻ, làm quiz theo học phần.

> Dự án target **`net8.0-windows`** nên **chỉ chạy trên Windows**.

---

## 1) Tổng quan dự án (phân tích nhanh)

### Module A — TOCFL Quiz Manager
- Quét dataset theo cấu trúc thư mục `Listening/` và `Reading/`
- Đọc đáp án từ `*_Answer.xlsx`
- Mở đề (PDF/ảnh) bằng **WebView2**
- Listening có **audio MP3** (NAudio) + có thể có **script PDF**
- Lưu tiến độ + lịch ôn tập (spaced repetition) vào `progress.json` trong LocalAppData

### Module B — Flashcards / Học phần
- Lưu học phần (set) theo dạng folder + `set.json`
- Import nhanh: dán dữ liệu, chọn separator, preview, lưu
- Học thẻ + ⭐ star từng thẻ (lưu `starred.json`)
- Quiz theo học phần (UI dạng thẻ)

---

## 2) Tính năng chính

### A. TOCFL Quiz (Listening/Reading)
- Quét dataset để tạo danh sách bài theo Mode/Category
- Hiển thị **PDF/ảnh đề** trong WebView2
- (Listening) phát **MP3**: play/pause, trackbar, thời lượng
- Trả lời A–D hoặc A–F (tuỳ Category)
- Điều hướng:
  - Prev/Next câu trong cùng file
  - Prev/Next bài trong cùng danh sách
- Submit → chấm điểm → cập nhật `progress.json` + lịch ôn tập

> Note: Có 2 Category đặc biệt trong code (`Paragraph Completion`, `Sentence Comprehension`) **không auto-next** khi chọn đáp án (nhưng vẫn bấm Next/Prev bình thường).

### B. Flashcards / Học phần
- Danh sách học phần dạng “tile”
- Tạo học phần mới:
  - Import nhanh từ text (copy/paste)
  - Cho chọn separator giữa Term/Definition và separator giữa các dòng/thẻ
  - Preview → Save
- Học thẻ:
  - Prev/Next
  - ⭐ star thẻ (lưu file theo set)
- Quiz theo set: chọn số câu, kiểu hiển thị “Trả lời bằng …”, nộp bài xem kết quả

---

## 3) Công nghệ & thư viện

- **.NET 8** + **WinForms**
- **Microsoft.Web.WebView2**: hiển thị PDF/HTML
- **NAudio**: phát MP3
- **ExcelDataReader**: đọc file `*_Answer.xlsx`

---

## 4) Yêu cầu hệ thống

- Windows 10/11
- Visual Studio 2022 (khuyến nghị) **hoặc** .NET 8 SDK
- **WebView2 Runtime** (Windows 11 thường có sẵn; nếu thiếu thì cài thêm)

---

## 5) Cài đặt & chạy

### Cách 1: Visual Studio
1. Mở `TocflQuiz.slnx`
2. Restore NuGet packages
3. Run (F5)

### Cách 2: Command line
```bash
cd TocflQuiz/TocflQuiz
dotnet restore
dotnet run


cấu trúc thư mục:
<img width="759" height="492" alt="image" src="https://github.com/user-attachments/assets/576390d1-c952-4830-81b6-7fad936f3cf4" />
link để ở đầu readme
