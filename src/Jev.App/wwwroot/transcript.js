export function scrollToEnd(element) {
    if (!element) {
        return;
    }

    element.scrollTop = element.scrollHeight;
}
