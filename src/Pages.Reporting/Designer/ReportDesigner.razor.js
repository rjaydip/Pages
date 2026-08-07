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

// Pointer move/resize on the SVG sheet. The designer runs on an InteractiveServer circuit, so
// streaming pointermove to the server would lag — the gesture runs locally and only the final
// rectangle crosses to C# on pointerup.
//
// Every coordinate here is a millimetre, because the sheet's viewBox user space IS millimetres:
// getScreenCTM().inverse() turns a client position into one directly, and it already accounts
// for zoom and browser zoom. No px/percent/point conversion exists in this file.
export function attachSvgSheet(root, dotnet) {
    let session = null;

    const toPoint = (svg, e) => {
        const p = svg.createSVGPoint();
        p.x = e.clientX;
        p.y = e.clientY;
        return p.matrixTransform(svg.getScreenCTM().inverse());
    };

    root.addEventListener('pointerdown', e => {
        const svg = e.target.closest('[data-svg-sheet]');
        if (!svg) return;
        const grip = e.target.closest('[data-band-resize]');
        const mover = e.target.closest('[data-band-move]');
        const elWrap = e.target.closest('[data-el-index]');
        const handle = e.target.closest('[data-handle]');
        if (!grip && !mover && !elWrap) return;

        const band = e.target.closest('[data-band-index]');
        if (!band) return;
        e.preventDefault();

        const start = toPoint(svg, e);
        // The lattice comes from the sheet rather than being recomputed here — the C# side
        // publishes it, so the grid you see, the step you snap to and the minimum you clamp to
        // cannot drift apart from three separate definitions.
        const stepMm = parseFloat(svg.dataset.stepMm || '2');
        const minBandMm = parseFloat(svg.dataset.minBandMm || '6');
        const marginLeftMm = parseFloat(svg.dataset.marginLeftMm || '0');
        const marginTopMm = parseFloat(svg.dataset.marginTopMm || '0');
        const contentWidthMm = parseFloat(svg.dataset.contentWidthMm || '0');
        if (mover) {
            // Band reorder: the whole band follows the pointer and an indicator shows where it
            // will land. Slots are derived from the rendered band boxes, so the geometry the
            // user sees is the geometry the drop uses.
            const bands = [...svg.querySelectorAll('[data-band-index]')].map(g => ({
                index: +g.dataset.bandIndex,
                node: g,
                y: bandY(g),
                h: parseFloat(g.querySelector('.rd-svgband-box').getAttribute('height')),
            }));
            session = {
                kind: 'bandmove', svg, start, stepMm,
                bandIndex: +band.dataset.bandIndex,
                node: band,
                y0: bandY(band),
                bands,
                indicator: svg.querySelector('[data-drop-indicator]'),
                contentWidth: contentWidthMm,
                marginLeft: marginLeftMm,
            };
            session.indicator?.classList.add('rd-on');
            // Capture on the label rect itself — it's the exact node carrying the @onclick that
            // selects the band. Per Pointer Events L3, a click during capture is retargeted to
            // the capture element, and Blazor's delegation walks UP from that element looking for
            // a handler; capturing on the root (or any ancestor) puts the handler out of reach and
            // the click falls through to the canvas's page-selection handler instead.
            mover.setPointerCapture(e.pointerId);
            return;
        }

        if (grip) {
            session = {
                kind: 'band', svg, start, stepMm, minBandMm,
                bandIndex: +band.dataset.bandIndex,
                node: band.querySelector('.rd-svgband-box'),
                h0: parseFloat(band.querySelector('.rd-svgband-box').getAttribute('height')),
            };
            // The grip has no click handler of its own; capturing on it (rather than the root)
            // still keeps a post-drag click from being retargeted onto some unrelated ancestor.
            grip.setPointerCapture(e.pointerId);
        } else {
            session = {
                kind: 'element', svg, start, stepMm,
                bandIndex: +band.dataset.bandIndex,
                elIndex: +elWrap.dataset.elIndex,
                node: elWrap,
                box: elWrap.querySelector('.rd-svgel-box'),
                edge: handle ? handle.dataset.handle : '',
                x0: transformX(elWrap), y0: transformY(elWrap),
                w0: parseFloat(elWrap.querySelector('.rd-svgel-box').getAttribute('width')),
                h0: parseFloat(elWrap.querySelector('.rd-svgel-box').getAttribute('height')),
                // The grid pattern tiles from the content corner (MarginLeft, MarginTop), but an
                // element's x/y are band-relative. The two only coincide on the vertical axis
                // when everything stacked above the band happens to sum to a whole number of
                // cells — publishing the band's own page-space Y lets pointermove convert
                // through page space instead of guessing.
                marginLeft: marginLeftMm,
                marginTop: marginTopMm,
                bandOriginY: parseFloat(band.dataset.bandY || '0'),
            };
            // elWrap is the node with the element's @onclick + stopPropagation — capturing here
            // (instead of the root) is what lets a completed drag's selection stick.
            elWrap.setPointerCapture(e.pointerId);
        }
    });

    root.addEventListener('pointermove', e => {
        if (!session) return;
        const now = toPoint(session.svg, e);
        const dx = now.x - session.start.x;
        const dy = now.y - session.start.y;
        const snap = v => e.altKey ? v : Math.round(v / session.stepMm) * session.stepMm;

        if (session.kind === 'band') {
            session.cur = Math.max(snap(session.h0 + dy), session.minBandMm);
            session.node.setAttribute('height', session.cur);
            return;
        }

        if (session.kind === 'bandmove') {
            const margin = session.marginLeft;
            session.node.setAttribute('transform', `translate(${margin}, ${session.y0 + dy})`);
            // Land before the first band whose midpoint the pointer is above; past them all,
            // land last. Comparing against midpoints is what makes the drop feel predictable.
            const target = session.bands.filter(b => b.index !== session.bandIndex)
                .findIndex(b => now.y < b.y + b.h / 2);
            const slot = target < 0 ? session.bands.length - 1 : target;
            session.cur = slot;
            const at = session.bands.filter(b => b.index !== session.bandIndex)[slot];
            // 0.7mm offset (user units — the SVG viewBox is millimetres): nudges the insertion
            // line clear of the band edge it sits against, so it reads as its own line rather
            // than merging with the neighbouring band's border stroke.
            const lineY = at ? at.y - 0.7 : session.bands[session.bands.length - 1].y
                + session.bands[session.bands.length - 1].h + 0.7;
            session.indicator?.setAttribute('x1', margin);
            session.indicator?.setAttribute('x2', margin + session.contentWidth);
            session.indicator?.setAttribute('y1', lineY);
            session.indicator?.setAttribute('y2', lineY);
            return;
        }

        // The grid pattern is anchored at (MarginLeft, MarginTop) while an element's x/y are
        // band-relative, so convert band-relative -> page space, round to the grid's own origin
        // on that axis, and convert back. The two axes have different origins on an asymmetric
        // page, which is why the origin is a parameter rather than one shared value.
        const snapAxis = (v, bandOrigin, gridOrigin) => e.altKey ? v
            : gridOrigin + Math.round((v + bandOrigin - gridOrigin) / session.stepMm) * session.stepMm - bandOrigin;
        const snapX = v => snapAxis(v, session.marginLeft, session.marginLeft);
        const snapY = v => snapAxis(v, session.bandOriginY, session.marginTop);

        let { x0: x, y0: y, w0: w, h0: h, edge } = session;
        if (!edge) {
            x = snapX(x + dx);
            y = snapY(y + dy);
        } else {
            if (edge.includes('e')) w = Math.max(snap(w + dx), session.stepMm);
            if (edge.includes('s')) h = Math.max(snap(h + dy), session.stepMm);
            // Clamp so the opposite edge (east/south) stays put at minimum size instead of the
            // handle sliding past it and dragging that "anchored" edge along for the ride.
            if (edge.includes('w')) {
                const nx = Math.min(snapX(x + dx), x + w - session.stepMm);
                w += x - nx; x = nx;
            }
            if (edge.includes('n')) {
                const ny = Math.min(snapY(y + dy), y + h - session.stepMm);
                h += y - ny; y = ny;
            }
        }
        session.cur = { x, y, w: Math.max(w, session.stepMm), h: Math.max(h, session.stepMm) };
        session.node.setAttribute('transform', `translate(${x}, ${y})`);
        session.box.setAttribute('width', session.cur.w);
        session.box.setAttribute('height', session.cur.h);
    });

    root.addEventListener('pointerup', async () => {
        if (!session) return;
        const s = session;
        session = null;
        if (s.cur === undefined) {
            // A click with no movement is a selection, not a drag — but the indicator was
            // switched on unconditionally at pointerdown, so it must be cleared here too or a
            // press-and-release on a band label leaves the drop line frozen on screen (Blazor
            // never rewrites it back, since its coordinates are static in the render tree).
            s.indicator?.classList.remove('rd-on');
            return;
        }

        // Restore whatever this gesture painted directly, before asking C# to commit. If the
        // model actually changes, Blazor's re-render overwrites these with the authoritative
        // values; if the commit is a no-op (dropped back in its own slot, an unchanged height, or
        // a clamp the JS itself didn't apply) Blazor emits no diff, and these restored values are
        // what the user is left looking at — instead of the JS-mutated ones nothing ever confirmed.
        if (s.kind === 'bandmove') {
            const margin = s.marginLeft;
            s.node.setAttribute('transform', `translate(${margin}, ${s.y0})`);
        } else if (s.kind === 'band') {
            s.node.setAttribute('height', s.h0);
        } else {
            s.node.setAttribute('transform', `translate(${s.x0}, ${s.y0})`);
            s.box.setAttribute('width', s.w0);
            s.box.setAttribute('height', s.h0);
        }

        const round = v => Math.round(v * 100) / 100;
        if (s.kind === 'bandmove') {
            s.indicator?.classList.remove('rd-on');
            await dotnet.invokeMethodAsync('MoveBandTo', s.bandIndex, s.cur);
        }
        else if (s.kind === 'band')
            await dotnet.invokeMethodAsync('CommitBandHeight', s.bandIndex, round(s.cur));
        else
            await dotnet.invokeMethodAsync('CommitPosition', s.bandIndex, s.elIndex,
                round(s.cur.x), round(s.cur.y), round(s.cur.w), round(s.cur.h));
    });

    // A cancelled pointer (device disconnect, browser gesture takeover, etc.) fires neither
    // pointerup's early return nor its restore — without this, session stays non-null and the
    // very next pointermove elsewhere resumes the drag with no button held.
    root.addEventListener('pointercancel', () => {
        if (!session) return;
        session.indicator?.classList.remove('rd-on');
        session = null;
    });

    // A band group is translate(MarginLeft, y); its y is the second number.
    const bandY = g => transformPair(g)[1];

    // translate(x, y) written by the component; read back rather than re-deriving from the CTM
    // so a drag starts from exactly the number the model holds.
    const transformPair = node =>
        (node.getAttribute('transform') || '').match(/-?[\d.]+/g)?.map(Number) ?? [0, 0];
    function transformX(node) { return transformPair(node)[0]; }
    function transformY(node) { return transformPair(node)[1]; }
}
