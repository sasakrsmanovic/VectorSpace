import { deflateSync } from 'node:zlib';

// Deterministic PNG test data, independent of both the C# decoder and the
// screenshot pixel reader. Avoids an opaque, repetitive Base64 source literal.
export function splitImage() {
  function chunk(type, content) {
    const name = Buffer.from(type, 'ascii');
    let crc = 0xffffffff;
    for (const value of Buffer.concat([name, content])) {
      crc ^= value;
      for (let bit = 0; bit < 8; bit++) crc = (crc >>> 1) ^ ((crc & 1) ? 0xedb88320 : 0);
    }
    const result = Buffer.alloc(content.length + 12);
    result.writeUInt32BE(content.length, 0); name.copy(result, 4); content.copy(result, 8);
    result.writeUInt32BE((crc ^ 0xffffffff) >>> 0, result.length - 4);
    return result;
  }
  const width = 240, height = 120, header = Buffer.alloc(13);
  header.writeUInt32BE(width, 0); header.writeUInt32BE(height, 4);
  header[8] = 8; header[9] = 2; // RGB8, no interlacing.
  const rows = Buffer.alloc(height * (1 + width * 3));
  for (let y = 0; y < height; y++)
    for (let x = 0; x < width; x++)
      rows[y * (1 + width * 3) + 1 + x * 3 + (x < width / 2 ? 0 : 2)] = 255;
  return Buffer.concat([
    Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
    chunk('IHDR', header), chunk('IDAT', deflateSync(rows)), chunk('IEND', Buffer.alloc(0))
  ]).toString('base64');
}
