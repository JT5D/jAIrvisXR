/**
 * Safety Guard — command safety checks and permission gates.
 * Blocks destructive operations. git push requires explicit session permission.
 */

const BLOCKED_PATTERNS = [
  /rm\s+(-rf?|--recursive)/i,
  /rmdir/i, /mkfs/i, /dd\s+if=/i,
  />\s*\/dev\//i, /chmod\s+777/i,
  /curl.*\|\s*(bash|sh|zsh)/i,
  /git\s+push/i,
  /git\s+reset\s+--hard/i,
  /git\s+clean\s+-f/i,
  /drop\s+(table|database)/i,
  /sudo/i,
  /launchctl\s+bootout/i,
];

let sessionPermissions = new Set();

export function checkCommandSafety(command) {
  const cmd = command.trim();

  // Check for git push with explicit permission
  if (/git\s+push/i.test(cmd) && sessionPermissions.has("push")) {
    return { safe: true };
  }

  for (const pattern of BLOCKED_PATTERNS) {
    if (pattern.test(cmd)) {
      return {
        safe: false,
        reason: `Blocked: matches safety pattern "${pattern.source}"`,
        pattern: pattern.source,
      };
    }
  }

  return { safe: true };
}

export function grantPermission(perm) {
  sessionPermissions.add(perm);
  return true;
}

export function revokePermission(perm) {
  sessionPermissions.delete(perm);
  return true;
}

export function listPermissions() {
  return [...sessionPermissions];
}

export function resetPermissions() {
  sessionPermissions.clear();
}
