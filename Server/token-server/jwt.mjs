/**
 * Minimal JWT HS256 implementation using Node.js built-in crypto.
 * No external dependencies.
 */

import { createHmac } from "node:crypto";

function base64url(data) {
  const str =
    typeof data === "string"
      ? Buffer.from(data).toString("base64")
      : data.toString("base64");
  return str.replace(/=/g, "").replace(/\+/g, "-").replace(/\//g, "_");
}

/**
 * Sign a JWT payload with HS256.
 * @param {object} payload - JWT claims
 * @param {string} secret  - HMAC secret (LiveKit API secret)
 * @returns {string} - Signed JWT string
 */
export function sign(payload, secret) {
  const header = { alg: "HS256", typ: "JWT" };

  const encodedHeader = base64url(JSON.stringify(header));
  const encodedPayload = base64url(JSON.stringify(payload));
  const signingInput = `${encodedHeader}.${encodedPayload}`;

  const signature = createHmac("sha256", secret)
    .update(signingInput)
    .digest();

  return `${signingInput}.${base64url(signature)}`;
}
