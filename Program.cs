﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Home_Finance_02
{
    class Program
    {
        static void Main(string[] args)
        {
            // Добавлен комментарий
            Console.WriteLine();
            HF_BD MyBD = new HF_BD();
            MainWindow W = new MainWindow();
            W.Start(MyBD);
        }
    }
}
