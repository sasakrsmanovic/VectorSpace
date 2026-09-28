import { inflateSync } from 'node:zlib';

// Test-only decoder for the trusted, 8-bit RGB/RGBA PNGs produced by Chromium screenshots.
export function pixels(png) {
  const parts = []; let width, height, channels;
  for (let p = 8; p + 12 <= png.length;) {
    const size = png.readUInt32BE(p), kind = png.toString('ascii', p + 4, p + 8), data = png.subarray(p + 8, p + 8 + size);
    if (kind === 'IHDR') {
      width = data.readUInt32BE(0); height = data.readUInt32BE(4); channels = data[9] === 2 ? 3 : data[9] === 6 ? 4 : 0;
      if (data[8] !== 8 || data[12] !== 0 || !channels) throw new Error('Expected non-interlaced RGB/RGBA screenshot');
    }
    if (kind === 'IDAT') parts.push(data);
    p += size + 12;
  }
  const raw = inflateSync(Buffer.concat(parts)), stride = width * channels, decoded = Buffer.alloc(stride * height);
  if (raw.length !== (stride + 1) * height) throw new Error('Unexpected screenshot dimensions');
  const paeth = (a, b, c) => { const p = a + b - c, x = Math.abs(p - a), y = Math.abs(p - b), z = Math.abs(p - c); return x <= y && x <= z ? a : y <= z ? b : c; };
  for (let y = 0; y < height; y++) {
    const type = raw[y * (stride + 1)];
    for (let x = 0; x < stride; x++) {
      const index = y * stride + x, a = x >= channels ? decoded[index - channels] : 0, b = y > 0 ? decoded[index - stride] : 0, c = y > 0 && x >= channels ? decoded[index - stride - channels] : 0;
      const predictor = [0, a, b, Math.floor((a + b) / 2), paeth(a, b, c)][type];
      if (predictor === undefined) throw new Error('Unsupported PNG filter');
      decoded[index] = raw[y * (stride + 1) + x + 1] + predictor;
    }
  }
  return (x, y) => { const p = (Math.round(y) * width + Math.round(x)) * channels; return [...decoded.subarray(p, p + 3)]; };
}
