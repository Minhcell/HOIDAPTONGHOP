# Vẽ biểu tượng quyển sách + tia sáng AI, lưu .ico (dùng lúc build, không để ảnh trong repo)
import sys
from PIL import Image, ImageDraw
S = 256
im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(im)
d.ellipse((6, 6, S - 6, S - 6), fill="#243141")
d.polygon([(52, 78), (124, 92), (124, 196), (52, 184)], fill="#FFFFFF")
d.polygon([(204, 78), (132, 92), (132, 196), (204, 184)], fill="#E8F0FB")
for y in (110, 130, 150):
    d.rectangle((64, y, 112, y + 7), fill="#1F5FA8")
cx, cy, r, k = 168, 132, 26, 8
d.polygon([(cx, cy - r), (cx + k, cy - k), (cx + r, cy), (cx + k, cy + k), (cx, cy + r), (cx - k, cy + k), (cx - r, cy), (cx - k, cy - k)], fill="#F2B33D")
im.save(sys.argv[1] if len(sys.argv) > 1 else "icon.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
