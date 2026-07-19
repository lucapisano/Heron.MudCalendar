export function scroll(element, top) {
    if (element)
        element.scrollTo(0, top);
}

export function getScrollbarWidth(element) {
    if (!element) return 0;
    return element.offsetWidth - element.clientWidth;
}
