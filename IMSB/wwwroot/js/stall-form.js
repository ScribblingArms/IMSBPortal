// IMSB stall application – 5 steps + review, Bootstrap 5.3 validation styling. English, Telugu or Hindi: every message
// goes through T() (window.IMSB_I18N); add new English messages to IMSB/ScriptStrings.cs and SharedResource.te.resx.
// Each step is checked in the browser, then saved to the SHGDetails row with "Save and continue"
// (the server checks it again). "Submit application" locks the row and returns the reference number.
(function () {
    'use strict';

    const form = document.getElementById('stall-form');
    if (!form) return;

    const steps = Array.from(form.querySelectorAll('.form-step'));
    const stepper = Array.from(document.querySelectorAll('.stepper li'));
    const reviewBox = document.getElementById('review');
    let at = 0;

    // Messages in the page's language: window.IMSB_I18N (from the layout, IMSB.ScriptStrings) maps the English text
    // to its translation; {0}, {1} are filled from the arguments
    const I18N = window.IMSB_I18N || {};
    const T = (s, ...args) => args.reduce((r, v, i) => r.split('{' + i + '}').join(v), I18N[s] || s);
    const locale = ({ te: 'te-IN', hi: 'hi-IN' })[document.documentElement.lang] || 'en-IN';

    /* ---------------- Validation helpers ---------------- */
    const optionalMark = T('(optional)').replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const stripOptional = (t) => t.replace(new RegExp('\\s*' + optionalMark + '\\s*$', 'i'), '').trim();

    const labelText = (f) => {
        if (f.type === 'radio' || (f.type === 'checkbox' && !f.name.startsWith('decl'))) {
            const lg = f.closest('fieldset')?.querySelector('legend');
            return lg ? stripOptional(lg.textContent) : '';
        }
        const l = f.id && document.querySelector('label[for="' + f.id + '"]');
        return l ? stripOptional(l.textContent) : (f.name || '');
    };

    // Element that carries the error message / invalid state
    const errHost = (f) => (f.type === 'radio' ? f.closest('fieldset') : f);

    const clearError = (f) => {
        const host = errHost(f);
        const id = (host.id || f.name) + '-error';
        document.getElementById(id)?.remove();
        host.classList.remove('is-invalid');
        const inputs = f.type === 'radio' ? host.querySelectorAll('input') : [f];
        inputs.forEach(i => {
            i.classList.remove('is-invalid');
            i.removeAttribute('aria-invalid');
            const ids = (i.getAttribute('aria-describedby') || '').split(' ').filter(x => x && x !== id);
            ids.length ? i.setAttribute('aria-describedby', ids.join(' ')) : i.removeAttribute('aria-describedby');
        });
    };

    const setError = (f, msg) => {
        const host = errHost(f);
        const id = (host.id || f.name) + '-error';
        const fb = document.createElement('div');
        fb.className = 'invalid-feedback d-block';
        fb.id = id;
        fb.innerHTML = '<i class="bi bi-exclamation-circle me-1" aria-hidden="true"></i><span class="visually-hidden"></span>';
        fb.querySelector('.visually-hidden').textContent = T('Error:') + ' ';
        fb.appendChild(document.createTextNode(msg));

        if (f.type === 'radio') {
            host.appendChild(fb);
            host.classList.add('is-invalid');
        } else if (f.type === 'checkbox') {
            f.closest('.form-check').appendChild(fb);
        } else {
            const group = f.closest('.input-group');
            (group || f).insertAdjacentElement('afterend', fb);
        }

        const inputs = f.type === 'radio' ? host.querySelectorAll('input') : [f];
        inputs.forEach(i => {
            i.classList.add('is-invalid');
            i.setAttribute('aria-invalid', 'true');
            i.setAttribute('aria-describedby', ((i.getAttribute('aria-describedby') || '') + ' ' + id).trim());
        });
    };

    const check = (f, scope) => {
        const d = f.dataset;
        if (f.type === 'radio') {
            if (!f.required) return '';
            return scope.querySelector('input[name="' + f.name + '"]:checked') ? '' : (d.msg || T('Select an option'));
        }
        if (f.type === 'checkbox') return f.required && !f.checked ? (d.msg || T('Tick this box to continue')) : '';
        if (f.type === 'file') {
            const files = Array.from(f.files || []);
            if (f.required && !files.length) return d.msg || T('Upload {0}', labelText(f).toLowerCase());
            const max = parseInt(d.maxFiles || '1', 10), mb = parseFloat(d.maxMb || '2');
            if (files.length > max) return T('Upload no more than {0} files', max);
            const accept = (f.getAttribute('accept') || '').split(',');
            for (const file of files) {
                if (accept[0] && !accept.includes(file.type)) {
                    return T(accept.includes('application/pdf') ? '{0} must be a JPG, PNG or PDF' : '{0} must be a JPG or PNG', file.name);
                }
                if (file.size > mb * 1024 * 1024) return T('{0} must be smaller than {1} MB', file.name, mb);
            }
            return '';
        }
        const v = f.value.trim();
        if (!v) {
            if (f.validity.badInput) return d.msgRange || T('Check {0}', labelText(f).toLowerCase());
            return f.required ? (d.msg || T('Enter {0}', labelText(f).toLowerCase())) : '';
        }
        if (f.validity.typeMismatch) return d.msgType || T('Enter a valid {0}', labelText(f).toLowerCase());
        // 'u' so patterns can use \p{L} (letters in any script, e.g. Telugu)
        if (f.pattern && !new RegExp('^(?:' + f.pattern + ')$', 'u').test(v)) return d.msgPattern || T('Check the format of {0}', labelText(f).toLowerCase());
        if ((f.type === 'number' || f.type === 'month') &&
            (f.validity.rangeUnderflow || f.validity.rangeOverflow || f.validity.badInput)) {
            return d.msgRange || T('Check {0}', labelText(f).toLowerCase());
        }
        // e.g. 5.5 members, or a price with more than 2 decimals
        if (f.type === 'number' && f.validity.stepMismatch) {
            return d.msgStep || d.msgRange || T('Enter a whole number for {0}', labelText(f).toLowerCase());
        }
        if (f.minLength > 0 && v.length < f.minLength) return d.msgShort || T('Enter at least {0} characters', f.minLength);
        if (d.gte) {
            const other = document.getElementById(d.gte);
            if (other && other.value && parseFloat(v) < parseFloat(other.value)) return d.msgGte || T('Check this value');
        }
        return '';
    };

    // Skip fields inside a hidden box *within* the step (conditional questions)
    const skipped = (f, scope) => {
        const h = f.closest('[hidden]');
        return !!(h && h !== scope && scope.contains(h));
    };

    const validate = (scope) => {
        const errors = [], seenRadio = {};
        scope.querySelectorAll('input, select, textarea').forEach(f => {
            if (f.type === 'hidden' || f.disabled || skipped(f, scope)) return;
            if (f.type === 'radio') {
                if (seenRadio[f.name]) return;
                seenRadio[f.name] = true;
                f = scope.querySelector('input[name="' + f.name + '"]');
            }
            clearError(f);
            const msg = check(f, scope);
            if (msg) { setError(f, msg); errors.push({ f, msg }); }
        });
        return errors;
    };

    const showSummary = (box, errors) => {
        if (!box) return;
        const ul = box.querySelector('ul');
        ul.innerHTML = '';
        errors.forEach(e => {
            const li = document.createElement('li');
            const a = document.createElement('a');
            a.href = '#' + e.f.id;
            a.className = 'alert-link';
            a.textContent = e.msg;
            a.addEventListener('click', ev => {
                ev.preventDefault();
                e.f.focus();
                e.f.scrollIntoView({ block: 'center' });
            });
            li.appendChild(a);
            ul.appendChild(li);
        });
        box.hidden = !errors.length;
        if (errors.length) box.focus();
    };

    // Re-check a field once the user starts correcting it
    const recheck = (e) => {
        const f = e.target;
        if (!f.matches('input, select, textarea')) return;
        const textLike = f.tagName === 'TEXTAREA' || (f.tagName === 'INPUT' && !/^(checkbox|radio|file)$/.test(f.type));
        if ((e.type === 'input') !== textLike) return;
        const target = f.type === 'radio' ? form.querySelector('input[name="' + f.name + '"]') : f;
        if (target.getAttribute('aria-invalid') !== 'true') return;
        const m = check(target, form);
        clearError(target);
        if (m) setError(target, m);
    };
    form.addEventListener('input', recheck);
    form.addEventListener('change', recheck);

    /* ---------------- Field behaviours ---------------- */
    // (the start month's min / max come from the server, in India time)

    // Digits only for mobile / PIN
    form.querySelectorAll('input[type="tel"], #rep-pin').forEach(i =>
        i.addEventListener('input', () => { i.value = i.value.replace(/\D/g, ''); }));

    // Show "which exhibitions" only when Yes is chosen
    form.querySelectorAll('[data-toggle-target]').forEach(r => {
        r.addEventListener('change', () => {
            const box = document.getElementById(r.dataset.toggleTarget);
            const checked = form.querySelector('input[name="' + r.name + '"]:checked');
            box.hidden = !checked || checked.value !== 'Yes';
        });
    });

    // File previews
    const fmtSize = (b) => (b / 1024 < 1024 ? Math.round(b / 1024) + ' KB' : (b / 1048576).toFixed(1) + ' MB');
    form.querySelectorAll('input[type="file"]').forEach(inp => {
        inp.addEventListener('change', () => {
            const list = document.getElementById(inp.id + '-previews');
            list.innerHTML = '';
            Array.from(inp.files).forEach(file => {
                const li = document.createElement('li');
                if (/^image\//.test(file.type)) {
                    const im = document.createElement('img');
                    im.alt = '';
                    im.src = URL.createObjectURL(file);
                    im.onload = () => URL.revokeObjectURL(im.src);
                    li.appendChild(im);
                } else {
                    const s = document.createElement('span');
                    s.className = 'file-doc';
                    s.innerHTML = '<i class="bi bi-file-earmark-pdf" aria-hidden="true"></i>';
                    li.appendChild(s);
                }
                const cap = document.createElement('span');
                cap.className = 'file-name';
                cap.textContent = file.name + ' (' + fmtSize(file.size) + ')';
                li.appendChild(cap);
                list.appendChild(li);
            });
        });
    });

    /* ---------------- Step navigation ---------------- */
    const go = (i, quiet) => {
        steps.forEach((s, k) => { s.hidden = k !== i; });
        stepper.forEach((li, k) => {
            li.classList.toggle('is-done', k < i);
            li.classList.toggle('is-current', k === i);
            k === i ? li.setAttribute('aria-current', 'step') : li.removeAttribute('aria-current');
        });
        at = i;
        if (quiet) return;
        window.scrollTo({ top: form.getBoundingClientRect().top + window.scrollY - 90 });
        steps[i].querySelector('.step-title')?.focus({ preventScroll: true });
    };

    // the visible label of a radio / checkbox: its value stays English for the server, the label is in the page's language
    const choiceLabel = (input) => document.querySelector('label[for="' + input.id + '"]')?.textContent.trim() || input.value;

    const valueOf = (scope, f) => {
        if (f.type === 'radio') {
            const chosen = scope.querySelector('input[name="' + f.name + '"]:checked');
            return chosen ? choiceLabel(chosen) : '';
        }
        if (f.type === 'checkbox' && f.name.startsWith('decl')) return f.checked ? T('Yes') : T('No');
        if (f.type === 'checkbox') return Array.from(scope.querySelectorAll('input[name="' + f.name + '"]:checked')).map(choiceLabel).join(', ');
        if (f.type === 'file') return Array.from(f.files).map(x => x.name).join(', ');
        if (f.tagName === 'SELECT') return f.value ? f.options[f.selectedIndex].text : '';
        if (f.type === 'month' && f.value) {
            const [y, m] = f.value.split('-');
            return new Date(y, m - 1, 1).toLocaleDateString(locale, { month: 'long', year: 'numeric' });
        }
        if (f.type === 'tel' && f.value) return '+91 ' + f.value.trim();
        if (f.name.startsWith('price') && f.value) return '₹' + f.value.trim();
        return f.value.trim();
    };

    const buildReview = () => {
        reviewBox.innerHTML = '';
        steps.slice(0, 5).forEach((s, k) => {
            const title = s.querySelector('.step-title').textContent;
            const card = document.createElement('div');
            card.className = 'card review-card';
            const head = document.createElement('div');
            head.className = 'card-header d-flex justify-content-between align-items-center';
            const h3 = document.createElement('h3');
            h3.className = 'h6 fw-semibold mb-0';
            h3.textContent = title;
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'btn btn-link btn-sm p-0 link-imsb fw-semibold';
            btn.innerHTML = '<i class="bi bi-pencil me-1" aria-hidden="true"></i><span></span><span class="visually-hidden"></span>';
            btn.children[1].textContent = T('Change');
            btn.children[2].textContent = ' ' + title;
            btn.addEventListener('click', () => go(k));
            head.append(h3, btn);

            const dl = document.createElement('dl');
            dl.className = 'row mb-0 card-body small';
            const seen = {};
            s.querySelectorAll('input, select, textarea').forEach(f => {
                if (f.type === 'hidden' || seen[f.name] || skipped(f, s)) return;
                seen[f.name] = true;
                const dt = document.createElement('dt');
                dt.className = 'col-sm-5 fw-semibold';
                dt.textContent = labelText(f);
                const dd = document.createElement('dd');
                dd.className = 'col-sm-7';
                const v = valueOf(s, f);
                dd.textContent = v || T('Not given');
                if (!v) dd.classList.add('text-body-secondary', 'fst-italic');
                dl.append(dt, dd);
            });
            card.append(head, dl);
            reviewBox.appendChild(card);
        });
    };

    /* ---------------- Server save (steps 1-5 -> SHGDetails table) ---------------- */
    const setSaving = (btn, saving) => {
        btn.disabled = saving;
        btn.querySelector('[data-saving]')?.classList.toggle('d-none', !saving);
    };

    const saveUrls = {
        shg: form.dataset.saveShgUrl,
        rep: form.dataset.saveRepUrl,
        product: form.dataset.saveProductUrl,
        stall: form.dataset.saveStallUrl,
        declaration: form.dataset.saveDeclarationUrl
    };

    const antiForgery = () => form.querySelector('input[name="__RequestVerificationToken"]').value;

    // POSTs the step's fields (and files). Resolves to [] on success, or a list of { f, msg } errors to show.
    const saveStep = async (step, kind) => {
        const fd = new FormData();
        fd.append('__RequestVerificationToken', antiForgery());
        const publicId = document.getElementById('shg-public-id');
        if (publicId.value) fd.append('shg-public-id', publicId.value);
        step.querySelectorAll('input, select, textarea').forEach(f => {
            if (!f.name || f.type === 'hidden') return;
            // only the chosen radio / ticked boxes are answers
            if ((f.type === 'radio' || f.type === 'checkbox') && !f.checked) return;
            if (f.type === 'file') Array.from(f.files).forEach(file => fd.append(f.name, file, file.name));
            else fd.append(f.name, f.value.trim());
        });

        let res, body = {};
        try {
            res = await fetch(saveUrls[kind], { method: 'POST', body: fd, headers: { 'Accept': 'application/json' } });
            body = await res.json().catch(() => ({}));
        } catch {
            return [{ f: step.querySelector('input, select'), msg: T('We could not reach the server. Check your connection and try again.'), general: true }];
        }

        if (res.ok && body.publicId) {
            publicId.value = body.publicId;
            return [];
        }
        if (res.status === 400 && body.errors) {
            const list = [];
            Object.entries(body.errors).forEach(([name, msg]) => {
                const f = step.querySelector('[name="' + name + '"]');
                if (f) { clearError(f); setError(f, msg); list.push({ f, msg }); }
                else list.push({ f: step.querySelector('input, select'), msg, general: true });
            });
            if (list.length) return list;
        }
        if (res.status === 413) {
            const f = step.querySelector('input[type="file"]') || step.querySelector('input, select');
            const msg = kind === 'declaration'
                ? T('The documents are too large to upload. Use files of 2 MB or less.')
                : T('The photos are too large to upload. Use up to 5 photos of 2 MB or less.');
            return [{ f, msg, general: true }];
        }
        return [{ f: step.querySelector('input, select'), msg: body.message || T('We could not save your details. Please try again.'), general: true }];
    };

    let busy = false;
    form.addEventListener('click', async (e) => {
        const next = e.target.closest('[data-step-next]');
        if (next) {
            if (busy) return;
            const step = steps[at];
            const summary = step.querySelector('.error-summary');
            const errors = validate(step);
            showSummary(summary, errors);
            if (errors.length) return;

            if (next.dataset.save) {
                busy = true;
                setSaving(next, true);
                try {
                    const serverErrors = await saveStep(step, next.dataset.save);
                    if (serverErrors.length) { showSummary(summary, serverErrors); return; }
                    announce(T('{0} saved', step.querySelector('.step-title').textContent));
                } finally {
                    busy = false;
                    setSaving(next, false);
                }
            }

            if (summary) summary.hidden = true;
            if (at === 4) buildReview();
            go(at + 1);
        }
        if (e.target.closest('[data-step-back]')) go(at - 1);
    });

    const announce = (msg) => {
        const el = document.getElementById('a11y-status');
        if (el) el.textContent = msg;
    };

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        // Enter in a field of steps 1-5 must not submit: changes there are only saved by "Save and continue"
        const reviewAt = steps.length - 1;
        if (at !== reviewAt || busy) return;

        // Final safety check of every step
        for (let k = 0; k < 5; k++) {
            const errs = validate(steps[k]);
            if (errs.length) {
                go(k);
                showSummary(steps[k].querySelector('.error-summary'), errs);
                return;
            }
        }

        const reviewSummary = steps[reviewAt].querySelector('.error-summary');
        const submitBtn = form.querySelector('button[type="submit"]');
        const fd = new FormData();
        fd.append('__RequestVerificationToken', antiForgery());
        fd.append('shg-public-id', document.getElementById('shg-public-id').value);

        busy = true;
        setSaving(submitBtn, true);
        let res, body = {};
        try {
            res = await fetch(form.dataset.submitUrl, { method: 'POST', body: fd, headers: { 'Accept': 'application/json' } });
            body = await res.json().catch(() => ({}));
        } catch {
            res = null;
        } finally {
            busy = false;
            setSaving(submitBtn, false);
        }

        if (!res || !res.ok || !body.reference) {
            const msg = !res ? T('We could not reach the server. Check your connection and try again.')
                : (body.message || T('We could not submit your application. Please try again in a few minutes.'));
            // a step that was never saved: take the user there
            if (body.step >= 1 && body.step <= 5) {
                const step = steps[body.step - 1];
                go(body.step - 1);
                showSummary(step.querySelector('.error-summary'), [{ f: step.querySelector('input, select, textarea'), msg }]);
            } else {
                showSummary(reviewSummary, [{ f: submitBtn, msg }]);
            }
            return;
        }

        document.getElementById('done-ref').textContent = body.reference;
        document.getElementById('done-mobile').textContent = '+91 ' + document.getElementById('rep-mobile').value;
        form.hidden = true;
        document.querySelector('.stepper').closest('.col-lg-3').hidden = true;
        document.querySelector('.stepper').closest('.row').querySelector('.col-lg-9').classList.add('mx-auto');
        const done = document.getElementById('form-done');
        done.hidden = false;
        window.scrollTo({ top: done.getBoundingClientRect().top + window.scrollY - 90 });
        done.querySelector('h2').focus({ preventScroll: true });
    });

    document.querySelector('[data-print]')?.addEventListener('click', () => window.print());

    go(0, true);
})();
