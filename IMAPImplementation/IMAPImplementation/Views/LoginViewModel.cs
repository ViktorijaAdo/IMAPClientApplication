using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EmailClient;
using System.Windows.Controls;

namespace IMAPImplementation.Views
{
    class LoginViewModel
    {
        public String DomainName { get; set; }
        public String Username { get; set; }

        public void OnSave(object parameter)
        {
            var passwordBox = (PasswordBox)parameter;
            IMAPClient client = new IMAPClient(DomainName);
            if (client.Connect(Username, passwordBox.Password))
            {
                MailBoxWindow mailBox = new MailBoxWindow(client);
                App.Current.MainWindow = mailBox;
            }
        }
    }
}
