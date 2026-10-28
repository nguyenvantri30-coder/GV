// ================================================================
// GOOGLE APPS SCRIPT: HỆ THỐNG QUẢN TRỊ ÔN TẬP & BÁO CÁO SƯ PHẠM
// Tác giả: ALSM Team & Thầy Tri | Phiên bản V2.5 Chuyên sâu
// ================================================================

const SPREADSHEET_ID = "11l7NlGZSWfNkiPaImIKZw552kMffknSTQFf49KacwL8";
const FOLDER_ID      = "1blXhHW57rTEZXuzS2ArNjgDF5xvLB-PD";

const STANDARD_SHEETS = [
  "OnTap_BaiHoc",
  "DeCuong_GK1",
  "DeCuong_CK1",
  "DeCuong_GK2",
  "DeCuong_CK2"
];

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
// HÀM HỖ TRỢ THƯ MỤC DRIVE
// ================================================================
function getOrCreateSubFolder(parentFolder, folderName) {
  const subFolders = parentFolder.getFoldersByName(folderName);
  if (subFolders.hasNext()) {
    return subFolders.next();
  }
  return parentFolder.createFolder(folderName);
}

// ================================================================
// 1. DOPOST: NHẬN BÀI NỘP HOẶC NHẬN ĐỒNG BỘ 1.829 HỌC SINH TỪ MÁY
// ================================================================
function doPost(e) {
  try {
    let p = e.parameter || {};
    let postData = null;

    if (e.postData && e.postData.contents) {
      try {
        postData = JSON.parse(e.postData.contents);
        if (typeof postData === "object" && !p.name) {
          p = postData;
        }
      } catch (ex) {}
    }

    // A. NHẬN LỆNH NẠP 1.829 HỌC SINH TỪ MÁY TÍNH BẮN LÊN
    if (p.action === "sync_students" && p.students) {
      return syncMasterStudents(p.students);
    }

    // B. NHẬN LỆNH DỌN DẸP
    if (p.action === "dondep") {
      dondep();
      return ContentService.createTextOutput("OK: Đã dọn dẹp hệ thống!").setMimeType(ContentService.MimeType.TEXT);
    }

    const mode = p.mode || "LESSON";
    const decuongType = p.decuongType || "";

    // C. Xác định Sheet mục tiêu trong 5 Sheet chuẩn
    let targetSheetName = "OnTap_BaiHoc";
    if (mode === "DECUONG" || decuongType) {
      if (decuongType.indexOf("GK1") !== -1) targetSheetName = "DeCuong_GK1";
      else if (decuongType.indexOf("CK1") !== -1) targetSheetName = "DeCuong_CK1";
      else if (decuongType.indexOf("GK2") !== -1) targetSheetName = "DeCuong_GK2";
      else if (decuongType.indexOf("CK2") !== -1) targetSheetName = "DeCuong_CK2";
    }

    const ss = SpreadsheetApp.openById(SPREADSHEET_ID);
    let sheet = ss.getSheetByName(targetSheetName);
    if (!sheet) {
      sheet = ss.insertSheet(targetSheetName);
      sheet.appendRow(HEADERS);
    }

    // D. Lưu ảnh vào Thư mục con của Google Drive
    let fileUrl = "Không có ảnh";
    const rawImage = p.image || "";
    if (rawImage && rawImage.indexOf("base64,") !== -1) {
      const base64Data = rawImage.split("base64,")[1];
      const decodedBytes = Utilities.base64Decode(base64Data);
      const cleanName = (p.name || "HS").replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1EA0-\u1EF9]/g, "_");
      const cleanClass = (p.class || "Lop").replace(/[^a-zA-Z0-9_]/g, "_");
      const fileName = `[${cleanClass}]_${cleanName}_${Date.now()}.jpg`;
      const blob = Utilities.newBlob(decodedBytes, "image/jpeg", fileName);

      const rootFolder = DriveApp.getFolderById(FOLDER_ID);
      const subFolder = getOrCreateSubFolder(rootFolder, targetSheetName);

      const file = subFolder.createFile(blob);
      file.setSharing(DriveApp.Access.ANYONE_WITH_LINK, DriveApp.Permission.VIEW);
      fileUrl = file.getUrl();
    }

    // E. Ghi 9 cột vào Sheet
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
// 2. DOGET: TRUY CẬP ĐIỀU KHIỂN BẰNG URL TRÊN TRÌNH DUYỆT
// ================================================================
function doGet(e) {
  const action = (e && e.parameter && e.parameter.action) ? e.parameter.action : "";

  if (action === "dashboard") {
    capNhatDashboard();
    return ContentService.createTextOutput("✅ ĐÃ CẬP NHẬT DASHBOARD THÀNH CÔNG!").setMimeType(ContentService.MimeType.TEXT);
  }
  if (action === "scan") {
    const res = quetGianLan();
    return ContentService.createTextOutput("✅ ĐÃ QUÉT GIAN LẬN: " + res).setMimeType(ContentService.MimeType.TEXT);
  }
  if (action === "dondep") {
    dondep();
    return ContentService.createTextOutput("✅ ĐÃ DỌN DẸP HỆ THỐNG THÀNH CÔNG!").setMimeType(ContentService.MimeType.TEXT);
  }

  return ContentService.createTextOutput("✅ Máy chủ ALSM Quiz đang hoạt động tốt. Sẵn sàng tiếp nhận dữ liệu!").setMimeType(ContentService.MimeType.TEXT);
}

// ================================================================
// 3. ĐỒNG BỘ 1.829 HỌC SINH TỪ VNEDU LÊN TAB DANHMUC_HOCSINH
// ================================================================
function syncMasterStudents(studentsList) {
  const ss = SpreadsheetApp.openById(SPREADSHEET_ID);
  let sheet = ss.getSheetByName("DanhMuc_HocSinh");
  if (!sheet) {
    sheet = ss.insertSheet("DanhMuc_HocSinh");
  }
  sheet.clear();

  // Header danh mục
  const headers = ["Mã học sinh", "Lớp", "Họ và tên", "Giới tính"];
  sheet.appendRow(headers);

  let rows = [];
  for (let i = 0; i < studentsList.length; i++) {
    const s = studentsList[i];
    rows.push([
      s.ma_hoc_sinh || "",
      (s.lop || "").toUpperCase(),
      s.ho_ten || "",
      s.gioi_tinh || ""
    ]);
  }

  if (rows.length > 0) {
    // Ghi hàng loạt (Batch write) cực nhanh
    sheet.getRange(2, 1, rows.length, 4).setValues(rows);
  }

  // Định dạng
  let hRange = sheet.getRange(1, 1, 1, 4);
  hRange.setBackground("#2d3748").setFontColor("#ffffff").setFontWeight("bold").setHorizontalAlignment("center");
  sheet.setFrozenRows(1);
  sheet.setColumnWidth(1, 140);
  sheet.setColumnWidth(2, 80);
  sheet.setColumnWidth(3, 200);
  sheet.setColumnWidth(4, 90);

  // Tự động làm mới Dashboard ngay sau khi nạp học sinh
  capNhatDashboard();

  return ContentService.createTextOutput("OK: Đã lưu " + rows.length + " học sinh vào DanhMuc_HocSinh!").setMimeType(ContentService.MimeType.TEXT);
}

// ================================================================
// 4. BẢNG ĐIỀU KHIỂN SƯ PHẠM (DASHBOARD) ĐA CHIỀU
// ================================================================
function capNhatDashboard() {
  const ss = SpreadsheetApp.openById(SPREADSHEET_ID);
  let dash = ss.getSheetByName("Dashboard");
  if (!dash) {
    dash = ss.insertSheet("Dashboard", 0);
  }
  dash.clear();

  // TIÊU ĐỀ CHÍNH
  dash.getRange("A1:G1").merge();
  dash.getRange("A1").setValue("📊 BẢNG ĐIỀU KHIỂN & THEO DÕI TIẾN ĐỘ NỘP BÀI")
      .setBackground("#1a365d").setFontColor("#ffffff").setFontWeight("bold")
      .setFontSize(14).setHorizontalAlignment("center").setVerticalAlignment("middle");
  dash.setRowHeight(1, 38);

  // --- PHẦN 1: TỔNG QUAN 5 ĐỢT ÔN TẬP ---
  const sumHeaders = ["Đợt ôn tập", "Số bài nộp", "Điểm TB", "Điểm cao nhất", "Số bài ≥ 8.0", "Tỉ lệ đạt (≥5)"];
  dash.getRange(3, 1, 1, sumHeaders.length).setValues([sumHeaders])
      .setBackground("#0f6cbf").setFontColor("#ffffff").setFontWeight("bold").setHorizontalAlignment("center");

  STANDARD_SHEETS.forEach(function(sName) {
    let s = ss.getSheetByName(sName);
    let count = 0, avg = 0, max = 0, gioi = 0, dat = "0%";
    if (s && s.getLastRow() > 1) {
      let scores = s.getRange(2, 6, s.getLastRow() - 1, 1).getValues().map(r => Number(r[0]) || 0);
      count = scores.length;
      if (count > 0) {
        let sum = scores.reduce((a, b) => a + b, 0);
        avg = (sum / count).toFixed(1);
        max = Math.max(...scores);
        gioi = scores.filter(x => x >= 8).length;
        dat = ((scores.filter(x => x >= 5).length / count) * 100).toFixed(0) + "%";
      }
    }
    dash.appendRow([sName, count, avg, max, gioi, dat]);
  });
  dash.getRange(3, 1, STANDARD_SHEETS.length + 1, sumHeaders.length).setBorder(true, true, true, true, true, true).setHorizontalAlignment("center");

  // --- PHẦN 2: THỐNG KÊ TIẾN ĐỘ THEO LỚP & SĨ SỐ (HÔM NAY) ---
  const startRowLop = 11;
  dash.getRange(`A${startRowLop}:G${startRowLop}`).merge();
  dash.getRange(`A${startRowLop}`).setValue("📋 TIẾN ĐỘ NỘP BÀI THEO TỪNG LỚP TRONG NGÀY HÔM NAY")
      .setBackground("#2b6cb0").setFontColor("#ffffff").setFontWeight("bold");

  const lopHeaders = ["Lớp", "Sĩ số (vnEdu)", "Đã nộp hôm nay", "Chưa nộp", "Tỉ lệ nộp (%)", "Điểm TB hôm nay", "Trạng thái"];
  dash.getRange(startRowLop + 1, 1, 1, lopHeaders.length).setValues([lopHeaders])
      .setBackground("#3182ce").setFontColor("#ffffff").setFontWeight("bold").setHorizontalAlignment("center");

  // Đọc danh mục học sinh gốc
  let dmSheet = ss.getSheetByName("DanhMuc_HocSinh");
  let classSiSo = {};
  let classHsMap = {}; // lop -> Set các tên học sinh
  if (dmSheet && dmSheet.getLastRow() > 1) {
    let dmData = dmSheet.getRange(2, 2, dmSheet.getLastRow() - 1, 2).getValues();
    dmData.forEach(r => {
      let l = (r[0] || "").toString().trim().toUpperCase();
      let name = (r[1] || "").toString().trim();
      if (l) {
        if (!classSiSo[l]) classSiSo[l] = 0;
        if (!classHsMap[l]) classHsMap[l] = [];
        classSiSo[l]++;
        classHsMap[l].push(name);
      }
    });
  }

  // Đọc dữ liệu nộp bài HÔM NAY trên 5 sheet
  let todayStr = Utilities.formatDate(new Date(), "GMT+7", "d/M/yyyy");
  let todaySubmitted = {}; // lop -> Set(tên học sinh)
  let todayScores = {};    // lop -> [mảng điểm]

  STANDARD_SHEETS.forEach(function(sName) {
    let s = ss.getSheetByName(sName);
    if (s && s.getLastRow() > 1) {
      let data = s.getRange(2, 1, s.getLastRow() - 1, 6).getValues();
      data.forEach(r => {
        let timeStr = (r[0] || "").toString();
        let lop = (r[1] || "").toString().trim().toUpperCase();
        let name = (r[2] || "").toString().trim();
        let score = Number(r[5]) || 0;

        if (timeStr.indexOf(todayStr) !== -1 && lop) {
          if (!todaySubmitted[lop]) todaySubmitted[lop] = new Set();
          if (!todayScores[lop]) todayScores[lop] = [];
          todaySubmitted[lop].add(name);
          todayScores[lop].push(score);
        }
      });
    }
  });

  // Ghi bảng tiến độ từng lớp
  let curRow = startRowLop + 2;
  let sortedClasses = Object.keys(classSiSo).sort();
  sortedClasses.forEach(function(lop) {
    let siso = classSiSo[lop] || 0;
    let danop = todaySubmitted[lop] ? todaySubmitted[lop].size : 0;
    let chuanop = Math.max(0, siso - danop);
    let pct = siso > 0 ? ((danop / siso) * 100).toFixed(0) + "%" : "0%";
    let avg = "-";
    if (todayScores[lop] && todayScores[lop].length > 0) {
      let total = todayScores[lop].reduce((a, b) => a + b, 0);
      avg = (total / todayScores[lop].length).toFixed(1);
    }
    let status = danop === siso && siso > 0 ? "🟢 Hoàn thành 100%" : (danop > 0 ? "🟡 Đang làm" : "⚪ Chưa nộp");

    dash.getRange(curRow, 1, 1, lopHeaders.length).setValues([[
      lop, siso, danop, chuanop, pct, avg, status
    ]]);
    curRow++;
  });

  if (sortedClasses.length > 0) {
    dash.getRange(startRowLop + 1, 1, sortedClasses.length + 1, lopHeaders.length)
        .setBorder(true, true, true, true, true, true).setHorizontalAlignment("center");
  }

  // Căn chỉnh độ rộng các cột
  dash.setColumnWidth(1, 170);
  dash.setColumnWidth(2, 110);
  dash.setColumnWidth(3, 110);
  dash.setColumnWidth(4, 110);
  dash.setColumnWidth(5, 110);
  dash.setColumnWidth(6, 110);
  dash.setColumnWidth(7, 140);

  Logger.log("✅ Đã cập nhật xong Dashboard đa chiều!");
}

// ================================================================
// 5. QUÉT PHÁT HIỆN GIAN LẬN
// ================================================================
function quetGianLan() {
  const ss = SpreadsheetApp.openById(SPREADSHEET_ID);
  let dash = ss.getSheetByName("Dashboard");
  if (!dash) { capNhatDashboard(); dash = ss.getSheetByName("Dashboard"); }

  let startRow = dash.getLastRow() + 3;
  dash.getRange(`A${startRow}:E${startRow}`).merge();
  dash.getRange(`A${startRow}`).setValue("⚠️ DANH SÁCH NGHI VẤN GIAN LẬN (1 Thiết bị làm thay nhiều tên HS)")
      .setBackground("#c53030").setFontColor("#ffffff").setFontWeight("bold");

  const alertHeaders = ["Mã thiết bị (Device ID)", "Số học sinh dùng chung", "Danh sách các học sinh", "Các lớp", "Đợt thi"];
  dash.getRange(startRow + 1, 1, 1, 5).setValues([alertHeaders])
      .setBackground("#e53e3e").setFontColor("#ffffff").setFontWeight("bold").setHorizontalAlignment("center");

  let deviceMap = {};
  STANDARD_SHEETS.forEach(function(sName) {
    let s = ss.getSheetByName(sName);
    if (s && s.getLastRow() > 1) {
      let data = s.getRange(2, 1, s.getLastRow() - 1, 8).getValues();
      data.forEach(r => {
        let dev = r[7];
        let hs = r[2];
        let lop = r[1];
        if (dev && dev !== "") {
          if (!deviceMap[dev]) deviceMap[dev] = { students: new Set(), classes: new Set(), sheets: new Set() };
          deviceMap[dev].students.add(hs);
          deviceMap[dev].classes.add(lop);
          deviceMap[dev].sheets.add(sName);
        }
      });
    }
  });

  let suspectRow = startRow + 2;
  let suspectCount = 0;
  for (let dev in deviceMap) {
    if (deviceMap[dev].students.size > 1) {
      dash.getRange(suspectRow, 1, 1, 5).setValues([[
        dev,
        deviceMap[dev].students.size,
        Array.from(deviceMap[dev].students).join(", "),
        Array.from(deviceMap[dev].classes).join(", "),
        Array.from(deviceMap[dev].sheets).join(", ")
      ]]);
      suspectRow++;
      suspectCount++;
    }
  }

  if (suspectCount === 0) {
    dash.getRange(suspectRow, 1, 1, 5).merge().setValue("✅ Không phát hiện thiết bị nào làm thay nhiều học sinh!")
        .setHorizontalAlignment("center").setFontColor("#2f855a");
  }

  return `Phát hiện ${suspectCount} thiết bị nghi vấn.`;
}

// ================================================================
// 6. DONDEP HỆ THỐNG
// ================================================================
function dondep() {
  const rootFolder = DriveApp.getFolderById(FOLDER_ID);
  const subFolders = rootFolder.getFolders();
  while (subFolders.hasNext()) {
    subFolders.next().setTrashed(true);
  }
  const files = rootFolder.getFiles();
  while (files.hasNext()) {
    files.next().setTrashed(true);
  }

  const ss = SpreadsheetApp.openById(SPREADSHEET_ID);
  STANDARD_SHEETS.forEach(function(sName) {
    let sheet = ss.getSheetByName(sName);
    if (!sheet) {
      sheet = ss.insertSheet(sName);
    } else {
      sheet.clear();
    }
    sheet.appendRow(HEADERS);
    let hRange = sheet.getRange(1, 1, 1, HEADERS.length);
    hRange.setBackground("#0f6cbf").setFontColor("#ffffff").setFontWeight("bold").setHorizontalAlignment("center");
    sheet.setRowHeight(1, 35);
    sheet.setFrozenRows(1);
  });

  capNhatDashboard();
}
