using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HomeFinance.ConsoleInterface;
using HomeFinance.AccountingSystem;

namespace HomeFinance
{
    class Program
    {
        static void Main(string[] args)
        {
            HF_BD MyBD = new HF_BD();
            MainWindow W = new MainWindow();
            W.Start(MyBD);
        }
    }
}
