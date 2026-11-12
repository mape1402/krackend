import { EditorState } from '@codemirror/state';
import { EditorView, lineNumbers, highlightActiveLineGutter, keymap } from '@codemirror/view';
import { HighlightStyle, syntaxHighlighting, foldGutter, foldKeymap } from '@codemirror/language';
import { json } from '@codemirror/lang-json';
import { defaultKeymap } from '@codemirror/commands';
import { searchKeymap, highlightSelectionMatches } from '@codemirror/search';
import { format, expandEmbedded } from './format-json.js';
import { tags } from '@lezer/highlight';

const colors = HighlightStyle.define([
  { tag: tags.propertyName, color: 'var(--od-json-key)' },
  { tag: tags.string, color: 'var(--od-json-string)' },
  { tag: tags.number, color: 'var(--od-json-number)' },
  { tag: tags.bool, color: 'var(--od-json-boolean)' },
  { tag: tags.null, color: 'var(--od-json-null)' },
  { tag: tags.punctuation, color: 'var(--od-json-punctuation)' }
]);

const mount = element => {
  if (element.dataset.editorMounted || !element.textContent.trim()) return;
  element.dataset.editorMounted = 'true';
  const text = element.textContent;
  element.textContent = '';
  const state = EditorState.create({ doc: format(text), extensions: [
    EditorState.readOnly.of(true), EditorView.editable.of(false),
    lineNumbers(), highlightActiveLineGutter(), foldGutter(),
    syntaxHighlighting(colors), json(),
    highlightSelectionMatches(), keymap.of([...defaultKeymap, ...searchKeymap, ...foldKeymap]),
    EditorView.lineWrapping,
    EditorView.theme({ '&': { height: '100%', backgroundColor: 'transparent', color: 'inherit' },
      '.cm-scroller': { overflow: 'auto', fontFamily: 'ui-monospace, SFMono-Regular, Consolas, monospace' },
      '.cm-content': { minWidth: '0' }, '.cm-gutters': { backgroundColor: 'transparent', color: 'inherit', opacity: '.65' } })
  ] });
  const view = new EditorView({ state, parent: element });
  const expanded = expandEmbedded(text);
  if (expanded !== text) {
    const button = document.createElement('button');
    button.type = 'button'; button.className = 'od-json-toggle'; button.textContent = 'Expand embedded JSON';
    button.addEventListener('click', () => {
      const isExpanded = button.dataset.expanded === 'true';
      view.dispatch({ changes: { from: 0, to: view.state.doc.length, insert: format(isExpanded ? text : expanded) } });
      button.dataset.expanded = String(!isExpanded);
      button.textContent = isExpanded ? 'Expand embedded JSON' : 'Show original JSON';
    });
    element.before(button);
  }
};

const observer = new IntersectionObserver(entries => entries.forEach(entry => {
  if (entry.isIntersecting) { mount(entry.target); observer.unobserve(entry.target); }
}), { rootMargin: '200px' });
document.querySelectorAll('[data-json-view]').forEach(element => {
  if (!element.textContent.trim()) { element.textContent = 'No data recorded.'; return; }
  observer.observe(element);
});
