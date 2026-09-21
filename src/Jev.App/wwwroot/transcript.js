export function scrollToEnd(element) {
    if (!element) {
        return;
    }

    element.scrollTop = element.scrollHeight;
}

/**
 * Copy text with the Clipboard API, then execCommand for WebViews that
 * reject navigator.clipboard.writeText (Photino / MAUI).
 */
export async function copyText(text) {
    const value = text == null ? "" : String(text);
    if (navigator.clipboard && typeof navigator.clipboard.writeText === "function") {
        try {
            await navigator.clipboard.writeText(value);
            return true;
        } catch {
            // fall through to the execCommand path
        }
    }

    return copyWithExecCommand(value);
}

function copyWithExecCommand(text) {
    const textarea = document.createElement("textarea");
    textarea.value = text;
    textarea.setAttribute("readonly", "");
    textarea.style.position = "fixed";
    textarea.style.left = "-9999px";
    textarea.style.top = "0";
    document.body.appendChild(textarea);
    textarea.focus();
    textarea.select();
    textarea.setSelectionRange(0, textarea.value.length);
    try {
        return document.execCommand("copy");
    } catch {
        return false;
    } finally {
        document.body.removeChild(textarea);
    }
}
