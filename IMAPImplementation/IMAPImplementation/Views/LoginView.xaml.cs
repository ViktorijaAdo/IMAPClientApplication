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
using System.Windows.Navigation;
using System.Windows.Shapes;
using EmailClient;

namespace IMAPImplementation.Views
{
    /// <summary>
    /// Interaction logic for LoginView.xaml
    /// </summary>
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            LoginView view = this as LoginView;
            
            IMAPClient client = new IMAPClient(view.Domain.Text);
            if (client.Connect(view.Username.Text, passwordBox.Password))
            {
                MailBoxWindow mailBox = new MailBoxWindow(client);
                App.Current.MainWindow = mailBox;
            }
        }
    }
}
