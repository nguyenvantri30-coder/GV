import json
import sys
import requests

WEB_APP_URL = "https://script.google.com/macros/s/AKfycbxW_OwAu199VbXqN8YmkIWa2ls6Q9l1kPasTtdHqmxt_qtytiB6NOoTU5T3Y56lkm18/exec"
MASTER_JSON_PATH = r"E:\Giao an\AGiaoan26_27\ontap_github\dshs_master.json"

def main():
    print("Reading dshs_master.json...")
    with open(MASTER_JSON_PATH, "r", encoding="utf-8-sig") as f:
        data = json.load(f)

    flat_list = []
    for class_name, students in data.items():
        for s in students:
            flat_list.append({
                "ma_hoc_sinh": s.get("MaHs", ""),
                "lop": s.get("LopTen", class_name),
                "ho_ten": s.get("HoTen", ""),
                "gioi_tinh": s.get("GioiTinh", "")
            })

    total = len(flat_list)
    print(f"Loaded {total} students across {len(data)} classes.")

    payload = {
        "action": "sync_students",
        "students": flat_list
    }

    print("Uploading to Google Sheet via Web App...")
    # Send as JSON POST
    resp = requests.post(WEB_APP_URL, json=payload, timeout=90)
    print("Response Status Code:", resp.status_code)
    print("Response Content (repr):", repr(resp.text))

if __name__ == "__main__":
    main()
