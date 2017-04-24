using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using EmailClient;
using IMAPImplementation.Views;

namespace IMAPImplementation
{
    /// <summary>
    /// Interaction logic for MailBoxWindow.xaml
    /// </summary>
    public partial class MailBoxWindow : Window
    {
        IMAPClient m_client = null;
        MailBoxView m_view = null;
        public MailBoxWindow(IMAPClient client)
        {
            m_client = client;
            m_view = new MailBoxView(client);
            InitializeComponent();
            this.RootGrid.Children.Add(m_view);
            this.Show();
        }
    }
}
