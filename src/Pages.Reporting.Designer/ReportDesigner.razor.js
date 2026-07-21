// Isolated JS module for the report designer. Its ONLY job is the HTML5 drag-and-drop
// handshake Blazor cannot do itself: dataTransfer must be populated during dragstart for
// drags to work cross-browser (Firefox requires it). All drag/drop *logic* stays in C#.
export function attach(root) {
    root.addEventListener('dragstart', e => {
        const source = e.target.closest('[draggable="true"]');
        if (source && e.dataTransfer) {
            e.dataTransfer.setData('text/plain', source.dataset.drag ?? 'pages-reporting');
            e.dataTransfer.effectAllowed = 'move';
        }
    });
}
