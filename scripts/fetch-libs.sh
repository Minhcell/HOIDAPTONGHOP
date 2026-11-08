#!/usr/bin/env bash
# Tải các thư viện chạy offline vào <thư mục web>/lib  (dùng trong GitHub Actions)
# Cách dùng: bash scripts/fetch-libs.sh web
set -euo pipefail
DEST="${1:-web}"
TMP="$(mktemp -d)"
pushd "$TMP" >/dev/null
npm init -y >/dev/null
npm install --no-audit --no-fund --ignore-scripts \
  @wllama/wllama@3.8.1 \
  tesseract.js@5.1.1 tesseract.js-core@5.1.1 @tesseract.js-data/vie@1.0.0 \
  mammoth@1.13.0 pdfjs-dist@3.11.174 >/dev/null
popd >/dev/null
N="$TMP/node_modules"
L="$DEST/lib"
mkdir -p "$L/tesseract/core" "$L/tesseract/lang" "$L/wllama"
cp "$N/mammoth/mammoth.browser.min.js"            "$L/"
cp "$N/pdfjs-dist/build/pdf.min.js"               "$L/"
cp "$N/pdfjs-dist/build/pdf.worker.min.js"        "$L/"
cp "$N/tesseract.js/dist/tesseract.min.js"        "$L/tesseract/"
cp "$N/tesseract.js/dist/worker.min.js"           "$L/tesseract/"
cp "$N"/tesseract.js-core/tesseract-core*.wasm.js "$L/tesseract/core/"
cp "$N/@tesseract.js-data/vie/4.0.0_best_int/vie.traineddata.gz" "$L/tesseract/lang/"
cp "$N/@wllama/wllama/esm/index.js"               "$L/wllama/"
cp "$N/@wllama/wllama/esm/wasm/wllama.wasm"       "$L/wllama/"
rm -rf "$TMP"
echo "Đã chép thư viện vào $L:"
find "$L" -type f | sort
