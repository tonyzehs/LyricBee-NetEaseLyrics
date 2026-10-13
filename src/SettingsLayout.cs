using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MusicBeePlugin
{
    // Rows derive their height from the controls; the host is a scrollable viewport.
    internal sealed class SettingsLayout
    {
        private readonly Panel _host;
        private readonly TableLayoutPanel _table;
        private readonly List<Label> _captions = new List<Label>();
        private readonly List<Label> _wrapped = new List<Label>();
        private readonly List<ComboBox> _choices = new List<ComboBox>();
        private readonly List<TableLayoutPanel> _inline = new List<TableLayoutPanel>();
        private readonly List<Control> _singleLine = new List<Control>();
        private int _row;
        private bool _reflowing;
        internal Panel Host => _host;

        internal SettingsLayout(Panel host)
        {
            _host = host; host.AutoScroll = true;
            _table = new TableLayoutPanel { Name = "SettingsLayout", Anchor = AnchorStyles.Top | AnchorStyles.Left, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, Margin = Padding.Empty,
                Padding = new Padding(0, 0, 8, 0), GrowStyle = TableLayoutPanelGrowStyle.AddRows };
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            host.Controls.Add(_table);
            EventHandler resize = (sender, e) => Reflow();
            host.ClientSizeChanged += resize;
            host.FontChanged += resize;
            _table.Disposed += (sender, e) => { host.ClientSizeChanged -= resize; host.FontChanged -= resize; };
        }

        internal void Add(Control control, int gap = 0, int minimumHeight = 0)
        {
            _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Margin = new Padding(0, 0, 0, gap);
            control.MinimumSize = new Size(0, minimumHeight);
            var singleLine = control is LinkLabel || control is Button || control is CheckBox;
            control.Anchor = singleLine ? AnchorStyles.Left : AnchorStyles.Left | AnchorStyles.Right;
            if (singleLine) _singleLine.Add(control);
            _table.Controls.Add(control, 0, _row++); _table.SetColumnSpan(control, 2);
            if (control is Label label && !(control is LinkLabel))
            {
                label.AutoSize = true; _wrapped.Add(label);
            }
        }

        internal void Pair(Label caption, ComboBox choice, int gap)
        {
            _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            caption.Margin = new Padding(0, 0, 8, gap); caption.Anchor = AnchorStyles.Left;
            choice.Margin = new Padding(0, 0, 0, gap); choice.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            choice.MinimumSize = new Size(140, choice.PreferredHeight);
            _table.Controls.Add(caption, 0, _row); _table.Controls.Add(choice, 1, _row++);
            _captions.Add(caption); _choices.Add(choice);
            choice.SelectedIndexChanged += (sender, e) => Reflow();
            choice.FontChanged += (sender, e) => Reflow();
        }

        internal void Inline(Label caption, ComboBox choice, int gap)
        {
            var row = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            caption.Margin = new Padding(0, 3, 12, 0); choice.Margin = Padding.Empty;
            choice.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            choice.MinimumSize = new Size(140, choice.PreferredHeight);
            row.Controls.Add(caption, 0, 0); row.Controls.Add(choice, 1, 0);
            _inline.Add(row); Add(row, gap); _choices.Add(choice);
            choice.SelectedIndexChanged += (sender, e) => Reflow();
            choice.FontChanged += (sender, e) => Reflow();
        }

        internal void Extras(CheckBox translation, CheckBox pronunciation)
        {
            var row = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            translation.Margin = new Padding(0, 0, Math.Max(12, 160 - translation.PreferredSize.Width), 0);
            pronunciation.Margin = Padding.Empty;
            row.Controls.Add(translation, 0, 0); row.Controls.Add(pronunciation, 1, 0); Add(row, 8);
            translation.Margin = new Padding(0, 0, Math.Max(12, 160 - translation.PreferredSize.Width), 0);
        }

        internal void Reflow()
        {
            if (_host.Disposing || _host.IsDisposed || _table.Disposing || _table.IsDisposed || _reflowing) return;
            _reflowing = true;
            _table.SuspendLayout();
            try
            {
                var captionWidth = Math.Max(130, _captions.Count == 0 ? 0 : _captions.Max(c => c.PreferredSize.Width + 12));
                _table.ColumnStyles[0].Width = captionWidth;
                var choiceWidth = _choices.Where(c => !(_inline.Any(r => c.Parent == r)))
                    .Select(c => TextRenderer.MeasureText(c.Text, c.Font).Width + SystemInformation.VerticalScrollBarWidth + 12)
                    .DefaultIfEmpty(140).Max();
                var minimum = captionWidth + Math.Max(140, choiceWidth) + 8;
                foreach (var control in _singleLine) minimum = Math.Max(minimum, control.PreferredSize.Width + 8);
                foreach (var row in _inline)
                    minimum = Math.Max(minimum, row.Controls[0].PreferredSize.Width + 12 +
                        TextRenderer.MeasureText(row.Controls[1].Text, row.Font).Width + SystemInformation.VerticalScrollBarWidth + 12);
                var width = Math.Max(minimum, _host.ClientSize.Width);
                _table.MinimumSize = new Size(width, 0);
                width -= 8;
                foreach (var label in _wrapped) label.MaximumSize = new Size(width, 0);
                foreach (var choice in _choices)
                {
                    choice.MinimumSize = new Size(140, choice.PreferredHeight);
                    choice.MaximumSize = new Size(Math.Max(360, TextRenderer.MeasureText(choice.Text, choice.Font).Width +
                        SystemInformation.VerticalScrollBarWidth + 12), 0);
                    choice.DropDownWidth = Math.Max(choice.Width, choice.Items.Cast<string>()
                        .Select(s => TextRenderer.MeasureText(s, choice.Font).Width).DefaultIfEmpty(0).Max() + SystemInformation.VerticalScrollBarWidth + 12);
                }
            }
            finally { _table.ResumeLayout(true); _reflowing = false; }
        }
    }
}
