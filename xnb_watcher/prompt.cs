using System;
using System.Drawing;
using System.Windows.Forms;

namespace xnb_watcher
{
    //https://stackoverflow.com/questions/5427020/prompt-dialog-in-windows-forms
    public static class Prompt
    {
        public static string ShowDialog(string text, string caption, string defaultText = "")
        {
            Form prompt = new Form()
            {
                Width = 700,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterScreen
            };
            Label textLabel = new Label() { Left = 10, Top = 20, Width = 400, Text = text };
            TextBox textBox = new TextBox() { Left = 10, Top = 50, Width = 660 };
            textBox.Text = defaultText;
            textBox.Font = new Font("Lucida Console", 10);
            Button confirmation = new Button() { Text = "Ok", Left = 570, Width = 100, Top = 80, DialogResult = DialogResult.OK };
            confirmation.Click += (sender, e) => { prompt.Close(); };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            prompt.ControlBox = false;
            prompt.StartPosition = FormStartPosition.CenterParent;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }
    }
}
