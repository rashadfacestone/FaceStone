$src = "f:\FaceStoneWebsite\scratch"
$dst = "C:\Users\win 10\.gemini\antigravity-ide\brain\05e50d0f-a203-4699-a1a7-79ba521eace6"

Copy-Item "$src\cm_verified_01_dashboard.png" -Destination $dst -Force
Copy-Item "$src\cm_verified_02_trans_details.png" -Destination $dst -Force
Copy-Item "$src\cm_verified_03_accept_confirm.png" -Destination $dst -Force
Copy-Item "$src\cm_verified_04_reject_reason.png" -Destination $dst -Force
Copy-Item "$src\cm_verified_05_storage_details.png" -Destination $dst -Force

Get-ChildItem "$dst\cm_verified_*.png" | Format-Table Name, Length
