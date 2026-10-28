// ================================================================
// GOOGLE APPS SCRIPT: HỆ THỐNG CHẤM ĐIỂM, GIÁM SÁT & DỌN DẸP ALSM
// Tích hợp cho: https://nguyenvantroithcs.github.io/GV/
// ================================================================

const SPREADSHEET_ID = "11l7NlGZSWfNkiPaImIKZw552kMffknSTQFf49KacwL8";
const FOLDER_ID      = "1blXhHW57rTEZXuzS2ArNjgDF5xvLB-PD";

// Danh sách 5 sheet chuẩn hóa toàn trường
const STANDARD_SHEETS = [
  "OnTap_BaiHoc",
  "DeCuong_GK1",
  "DeCuong_CK1",
  "DeCuong_GK2",
  "DeCuong_CK2"
];

// 9 Tiêu đề cột chuẩn phục vụ thống kê & chống gian lận
const HEADERS = [
  "Thời gian nộp bài",
  "Lớp",
  "Họ và tên",
  "Môn học",
  "Nội dung / Đề bài",
  "Điểm số",
  "Số câu đúng",
  "Mã thiết bị",
  "Link ảnh xác thực"
];

// ================================================================
// 1. HÀM DOPOST: HỨNG DỮ LIỆU TỪ WEB APP (INDEX.HTML) KHI HỌC SINH NỘP
// ================================================================
function doPost(e) {
  try {
    // Đọc tham số gửi lên từ FormData
    let p = e.parameter || {};

    // Nếu gửi bằng JSON payload thô (phòng ngừa)
    if (!p.name && e.postData && e.postData.contents) {
      try {
        p = JSON.parse(e.postData.contents);
      } catch (ex) {}
    }

    // Nếu gọi lệnh dọn dẹp qua POST
    if (p.action === "dondep") {
      dondep();
      return ContentService.createTextOutput("OK: Đã dọn dẹp hệ thống!").setMimeType(ContentService.MimeType.TEXT);
    }

    const mode = p.mode || "LESSON";
    const decuongType = p.decuongType || "";

    // A. Xác định đúng Sheet hứng dữ liệu trong 5 Sheet chuẩn
    let targetSheetName = "OnTap_BaiHoc";
    if (mode === "DECUONG" || decuongType) {
      if (decuongType.indexOf("GK1") !== -1) targetSheetName = "DeCuong_GK1";
      else if (decuongType.indexOf("CK1") !== -1) targetSheetName = "DeCuong_CK1";
      else if (decuongType.indexOf("GK2") !== -1) targetSheetName = "DeCuong_GK2";
      else if (decuongType.indexOf("CK2") !== -1) targetSheetName = "DeCuong_CK2";
    } else if (p.sheetName && STANDARD_SHEETS.indexOf(p.sheetName) !== -1) {
      targetSheetName = p.sheetName;
    }

    const ss = SpreadsheetApp.openById(SPREADSHEET_ID);
    let sheet = ss.getSheetByName(targetSheetName);
    if (!sheet) {
      sheet = ss.insertSheet(targetSheetName);
      sheet.appendRow(HEADERS);
    }

    // B. Lưu ảnh Camera giám sát vào Thư mục Google Drive
    let fileUrl = "Không có ảnh";
    const rawImage = p.image || "";
    if (rawImage && rawImage.indexOf("base64,") !== -1) {
      const base64Data = rawImage.split("base64,")[1];
      const decodedBytes = Utilities.base64Decode(base64Data);
      const cleanName = (p.name || "HS").replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1EA0-\u1EF9]/g, "_");
      const cleanClass = (p.class || "Lop").replace(/[^a-zA-Z0-9_]/g, "_");
      const fileName = `${cleanClass}_${cleanName}_${Date.now()}.jpg`;
      const blob = Utilities.newBlob(decodedBytes, "image/jpeg", fileName);

      const targetFolderId = p.folderId || FOLDER_ID;
      const folder = DriveApp.getFolderById(targetFolderId);
      const file = folder.createFile(blob);
      file.setSharing(DriveApp.Access.ANYONE_WITH_LINK, DriveApp.Permission.VIEW);
      fileUrl = file.getUrl();
    }

    // C. Ghi đúng 9 cột dữ liệu vào Sheet
    sheet.appendRow([
      p.time || new Date().toLocaleString("vi-VN"),
      p.class || "",
      p.name || "",
      p.mon || "",
      p.lessonTitle || "",
      p.score || 0,
      p.correct || "",
      p.device || "",
      fileUrl
    ]);

    return ContentService.createTextOutput("OK").setMimeType(ContentService.MimeType.TEXT);

  } catch (err) {
    return ContentService.createTextOutput("ERROR: " + err.toString()).setMimeType(ContentService.MimeType.TEXT);
  }
}

// ================================================================
// 2. HÀM DOGET: TEST TRẠNG THÁI HOẶC GỌI DỌN DẸP TRỰC TIẾP TỪ TRÌNH DUYỆT
// ================================================================
function doGet(e) {
  if (e && e.parameter && e.parameter.action === "dondep") {
    dondep();
    return ContentService.createTextOutput("✅ ĐÃ DỌN DẸP THÀNH CÔNG: Đã xóa ảnh cũ và tái tạo 5 sheet chuẩn!").setMimeType(ContentService.MimeType.TEXT);
  }
  return ContentService.createTextOutput("✅ Hệ thống Web App ALSM Quiz đang hoạt động tốt. Sẵn sàng tiếp nhận bài nộp!").setMimeType(ContentService.MimeType.TEXT);
}

// ================================================================
// 3. HÀM DONDEP: DỌN SẠCH ẢNH DRIVE + TÁI TẠO 5 SHEET CHUẨN + XÓA SHEET RÁC
// ================================================================
function dondep() {
  Logger.log("=== BẮT ĐẦU DỌN DẸP TOÀN BỘ HỆ THỐNG ===");

  // 1. Quét và chuyển toàn bộ file ảnh cũ trong thư mục Drive vào thùng rác
  const folder = DriveApp.getFolderById(FOLDER_ID);
  const files = folder.getFiles();
  let deletedCount = 0;

  while (files.hasNext()) {
    const file = files.next();
    file.setTrashed(true); // Đưa an toàn vào thùng rác
    deletedCount++;
  }
  Logger.log("🗑️ Đã xóa " + deletedCount + " file ảnh cũ trong Google Drive!");

  // 2. Tái cấu trúc Google Sheet
  const ss = SpreadsheetApp.openById(SPREADSHEET_ID);

  // Tạo mới hoặc làm sạch 5 sheet chuẩn trước
  STANDARD_SHEETS.forEach(function(sheetName) {
    let sheet = ss.getSheetByName(sheetName);
    if (!sheet) {
      sheet = ss.insertSheet(sheetName);
      Logger.log("➕ Tạo mới sheet: " + sheetName);
    } else {
      sheet.clear(); // Xóa sạch dữ liệu cũ
      Logger.log("🧹 Làm sạch sheet: " + sheetName);
    }

    // Đặt Header 9 cột
    sheet.appendRow(HEADERS);

    // Format giao diện Header: Nền xanh, chữ trắng đậm, cố định dòng 1
    const headerRange = sheet.getRange(1, 1, 1, HEADERS.length);
    headerRange.setBackground("#0f6cbf");
    headerRange.setFontColor("#ffffff");
    headerRange.setFontWeight("bold");
    headerRange.setHorizontalAlignment("center");
    headerRange.setVerticalAlignment("middle");
    sheet.setRowHeight(1, 36);
    sheet.setFrozenRows(1);

    // Set độ rộng cột trực quan
    sheet.setColumnWidth(1, 230); // Thời gian nộp bài + thời gian làm
    sheet.setColumnWidth(2, 80);  // Lớp
    sheet.setColumnWidth(3, 190); // Họ và tên
    sheet.setColumnWidth(4, 110); // Môn học
    sheet.setColumnWidth(5, 260); // Nội dung / Đề bài
    sheet.setColumnWidth(6, 80);  // Điểm số
    sheet.setColumnWidth(7, 100); // Số câu đúng
    sheet.setColumnWidth(8, 140); // Mã thiết bị
    sheet.setColumnWidth(9, 250); // Link ảnh xác thực
  });

  // Xóa các sheet cũ/rác ngoài 5 sheet chuẩn (như Sheet1, ToanHoc6A1...)
  const allSheets = ss.getSheets();
  let deletedSheets = 0;
  allSheets.forEach(function(sheet) {
    const name = sheet.getName();
    if (STANDARD_SHEETS.indexOf(name) === -1) {
      ss.deleteSheet(sheet);
      Logger.log("❌ Đã xóa sheet rác: " + name);
      deletedSheets++;
    }
  });

  Logger.log("🎉 HOÀN TẤT DỌN DẸP! Đã dọn " + deletedCount + " ảnh và xóa " + deletedSheets + " sheet thừa.");
}
