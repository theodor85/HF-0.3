using System;
using System.Collections.Generic;
using System.Globalization;
using HomeFinance.AccountingSystem;

//паттерн Шаблоный метод
namespace HomeFinance.ConsoleInterface
{
    public abstract class Window
    { 
        public void Start()
        {
            bool CloseWindow = false;  // если истина, то окно закрывается 
            string Parametres;        // параметры для обмена данными между методами

            do
            {
                Parametres = "";
                Console.Clear();
                Head();
                Outputs();
                CloseWindow = Choice(ref Parametres, Menu());
                if (CloseWindow)
                    break;
                else
                    Inputs(ref Parametres);
                Message(ref Parametres);

            } while (!CloseWindow);
        }

        protected abstract void Head();    // шапка
        protected abstract void Outputs(); // вывод отчетов в окне
        protected abstract void Inputs(ref string Parametres);  // ввод данных
        protected abstract string Menu();  // вывод меню
        protected abstract bool Choice(ref string Parametres, string MenuItem);  // обработка выбора пункта меню, возвращает true - если надо завершить работу
        protected abstract void Message(ref string Parametres); // сообщение об ошибке или об успешном выполнении

        protected void ReportOutput(List<string> ItemsList)
        {
            foreach (string item in ItemsList)
                Console.WriteLine("\t{0}", item);
            Console.WriteLine();
        }
        protected void ReportOutput(List<TItemState> Report)
        {

            double Itogo = 0;
            foreach (TItemState item in Report)
            {
                Itogo = Itogo + item.Sum;

                string ItemName = item.ItemName;
                string strSum = item.Sum.ToString("#####.00");

                int Tab = 56 - ItemName.Length - strSum.Length;
                string strTab = "";
                for (int i = 0; i < Tab; i++) strTab = strTab + " ";

                Console.WriteLine("\t{0}{1}{2}", ItemName, strTab, strSum);
            }

            int tab2 = 52 - Itogo.ToString("#####.00").Length;
            string strtab1 = "     ";
            string strtab2 = "";
            for (int i = 0; i < tab2; i++) strtab2 = strtab2 + " ";

            Console.WriteLine();
            Console.WriteLine("{1}Всего: {2}{0}", Itogo.ToString("#####.00"), strtab1, strtab2);
            Console.WriteLine();
        }
        // отчет об остатках
        protected void ReportBalance()
        {

        }
        // отчет о раходах
        protected void ReportExpenses()
        {

        }
        // Отчeт о доходах
        protected void ReportIncomes()
        {

        }

        // вывести список операций
        protected void ReportOperationsList(DateTime StartDate, DateTime EndDate)
        {
            Operation OperationObject = new Operation();
            List<TOperation> OperationsList = OperationObject.GetList(StartDate, EndDate);
            
            Console.WriteLine("");
            Console.WriteLine("ID\tДата\t\tТип\t\tИсточник\tПриемник\tСумма");
            Console.WriteLine("-------------------------------------------------------------------------------");

            foreach (TOperation Op in OperationsList)
            {
                string tabs1 = "\t";
                string tabs2 = "\t\t";
                string Source;
                string Dest;

                // форматирование таблицы
                if (Op.Source.Length < 8)
                    Source = Op.Source + tabs2;
                else
                    Source = Op.Source + tabs1;
                if (Op.Destination.Length < 8)
                    Dest = Op.Destination + tabs2;
                else
                    Dest = Op.Destination + tabs1;

                if (Op.OperationType == "Transfer")
                    Source = tabs1 + Source;
                else
                    Source = tabs2 + Source;

                Console.WriteLine("{0}\t{1}\t{2}" + Source + Dest + "{3}", Op.ID.ToString(), Op.Date.ToString("dd.MM.yyyy"), Op.OperationType.ToString(), Op.Sum.ToString("#####.00", CultureInfo.CreateSpecificCulture("en-US")));
                
            };

            Console.WriteLine("-------------------------------------------------------------------------------");
            
        }
    }

    public class MainWindow : Window
    {
        protected override void Head()
        {
            // выводим шапку
            Console.WriteLine("*****************************************************************");
            Console.WriteLine("*****************************************************************");
            Console.WriteLine("Home Finance v0.3.010 03/12/2017 by Teddy Coder.");
            Console.WriteLine("Добро пожаловать!");
            Console.WriteLine();
        }
        protected override void Outputs()
        {
            int CurrentMonth = DateTime.Today.Month;
            int CurrentYear = DateTime.Today.Year;
            int NumberOfDaysInMonth = DateTime.DaysInMonth(CurrentYear, CurrentMonth);
            DateTime Today = DateTime.Today;
            /*   DateTime BeginMonth = new DateTime(CurrentYear, CurrentMonth, 1);
               DateTime EndMonth = new DateTime(CurrentYear, CurrentMonth, NumberOfDaysInMonth);  */
            DateTime BeginMonth = new DateTime(2017, 10, 1);
            DateTime EndMonth = new DateTime(2017, 10, 31);

            // выводим отчет об остатках
            Console.WriteLine("-----------------------------------------------------------------");
            Console.WriteLine("На {0} у вас денег в кошельках: ", Today);
            Console.WriteLine();

            RegisterRestReports RR_Balance = new RegisterRestReports("Wallets");
            List<TItemState> ReportMoneyInWallets = RR_Balance.ReportRests(Today);
            ReportOutput(ReportMoneyInWallets);

            // выводим отчет о расходах
            Console.WriteLine("-----------------------------------------------------------------");
            Console.WriteLine("Ваши РАСХОДЫ за период {0}.{1}: ", CurrentMonth, CurrentYear);
            Console.WriteLine();
            RegisterTurnoverReports RT_ReportsExpenses = new RegisterTurnoverReports("Expenses");
            List<TItemState> ReportExpenses = RT_ReportsExpenses.ReportTurnover(BeginMonth, EndMonth);
            ReportOutput(ReportExpenses);

            // выводим отчет о доходах
            Console.WriteLine("-----------------------------------------------------------------");
            Console.WriteLine("Ваши ДОХОДЫ за период {0}.{1}: ", CurrentMonth, CurrentYear);
            Console.WriteLine();

            RegisterTurnoverReports RT_ReportsIncomes = new RegisterTurnoverReports("Incomes");
            List<TItemState> ReportIncomes = RT_ReportsIncomes.ReportTurnover(BeginMonth, EndMonth);
            ReportOutput(ReportIncomes);
        }
        protected override void Inputs(ref string Parametres) { }
        protected override void Message(ref string Parametres) { }
        protected override string Menu()
        {
            Console.WriteLine("-----------------------------------------------------------------");
            Console.WriteLine("Список доступных команд:");
            Console.WriteLine("0 - Выход; 1 - Ввод операций; 2 - Добавление элементов справочников; 3 - Список операций/Удалить операцию");
            Console.WriteLine("-----------------------------------------------------------------");
            Console.Write("--->");
            return Console.ReadLine();
        }

        // возвращает true для возврата
        protected override bool Choice(ref string Parametres, string MenuItem)
        {
            switch (MenuItem)
            {
                case "0":
                    return true;
                case "1":
                    EnterOperations WOp = new EnterOperations();
                    WOp.Start();
                    break;
                case "2":
                    AddReferenceItems WRefs = new AddReferenceItems();
                    WRefs.Start();
                    break;
                case "3":
                    OperationsList OpLst = new OperationsList();
                    OpLst.Start();
                    break;
                default:
                    Console.WriteLine("Введена недопустимая команда!");
                    break;
            }
            return false;
        }
    }

    public class EnterOperations : Window
    {

        protected override void Head()    // шапка
        {
            Console.WriteLine("*****************************************************************");
            Console.WriteLine("Вы находитесь в окне ввода операций");
            Console.WriteLine();
        }
        protected override void Outputs() // вывод отчетов в окне
        {

        }
        protected override void Inputs(ref string Parametres)   // ввод данных
        {
            // надписи
            string HeadLine;
            string SourceLine;
            string DestinationLine;

            // вводимые данные
            DateTime DT;
            string Source;
            string Destination;
            double Sum;
            OperationTypes TypeOfTheOperation;

            switch (Parametres)
            {
                case "1":   // ввод расходов
                    HeadLine = "Ввод расходов";
                    SourceLine = "Кошелек";
                    DestinationLine = "Статья расходов";
                    TypeOfTheOperation = OperationTypes.Expense;
                    break;
                case "2":   // ввод Доходов
                    HeadLine = "Ввод доходов";
                    SourceLine = "Статья доходов";
                    DestinationLine = "Кошелек";
                    TypeOfTheOperation = OperationTypes.Income;
                    break;
                case "3":   // ввод перемещение
                    HeadLine = "Ввод перемещения";
                    SourceLine = "Кошелек-источник";
                    DestinationLine = "Кошелек-приемник";
                    TypeOfTheOperation = OperationTypes.Transfer;
                    break;
                case "4":   // ввод начальных остатков
                    HeadLine = "Ввод начальных остатков";
                    SourceLine = "";
                    DestinationLine = "Кошелек";
                    TypeOfTheOperation = OperationTypes.Rest;
                    break;
                default:
                    HeadLine = "";
                    SourceLine = "";
                    DestinationLine = "";
                    TypeOfTheOperation = OperationTypes.Expense;
                    return;
            }
                

            do
            {
                //Console.Clear();
                Console.WriteLine("-----------------------------------------------------------------");
                Console.WriteLine(HeadLine);
                Console.WriteLine();

                // вывод кошельков

                // вывод статей расходов

                // ввод данных  
                Console.Write("Дата ------------->");
                try
                {
                    DT = DateTime.Parse(Console.ReadLine());
                }
                catch (FormatException e)
                {
                    Console.WriteLine("Недопустимый формат даты. Системное сообщение: " + e.Message);
                    Console.WriteLine("Нажмите Enter для продложения.");
                    Console.ReadLine();
                    break;
                }

                string tab = SourceLine;
                for (int i = SourceLine.Length; i < 18; i++)
                    tab = tab + "-";
                tab = tab + ">";

                Console.Write(tab);
                Source = Console.ReadLine();

                tab = DestinationLine;
                for (int i = DestinationLine.Length; i < 18; i++)
                    tab = tab + "-";
                tab = tab + ">";

                Console.Write(tab);
                Destination = Console.ReadLine();
                Console.Write("Сумма ------------>");
                try
                {
                    Sum = double.Parse(Console.ReadLine());
                }
                catch (FormatException e)
                {
                    Console.WriteLine("Недопустимый формат числа. Системное сообщение: " + e.Message);
                    Console.WriteLine("Нажмите Enter для продложения.");
                    Console.ReadLine();
                    break;
                }
                // для отладки
                /*DT = new DateTime(2017, 11, 28);
                Source = "Основной";
                Destination = "Еда";
                Sum = 10.3;
                Console.Write("Дата ------------->");
                Console.WriteLine(DT);
                Console.Write("Кошелёк ---------->");
                Console.WriteLine(Source);
                Console.Write("Статья расходов ---------->");
                Console.WriteLine(Destination);
                Console.Write("Сумма ------------>");
                Console.WriteLine(Sum);
                Console.ReadLine();*/
                // для отладки

                try
                {
                    //  DB.EnterOperation(TypeOfTheOperation, DT, Source, Destination, Sum);
                    Operation Op = new Operation();
                    Op.Source = Source;
                    Op.Destination = Destination;
                    Op.OperationDate = DT;
                    Op.TypeOfTheOperation = TypeOfTheOperation;
                    Op.Enter(Sum);
                    Console.WriteLine();
                    Console.WriteLine("Операция успешно завершена! Нажмите Enter для продложения.");
                    Console.WriteLine();
                    Console.ReadLine();
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                    Console.WriteLine("ERROR: Во время совершения операции проихошла ошибка!");
                    Console.WriteLine("Источник ошибки: {0}\n Cообщение: {1}", e.Source, e.Message);
                    Console.WriteLine("Нажмите Enter для продложения.");
                    Console.ReadLine();
                }

                Console.WriteLine("Продолжить ввод? Нет - 0; да - любая клавиша");
                Console.Write("--->");
                if (Console.ReadLine() == "0")
                    break;

            } while (true);
        }
        protected override string Menu()  // вывод меню
        {
            Console.WriteLine("-----------------------------------------------------------------");
            Console.WriteLine("Список доступных команд:");
            Console.WriteLine("0 - Выход; 1 - Ввод расходов; 2 - Ввод доходов; 3 - Перемещение; 4 - Ввод начальных остатков");
            Console.WriteLine("-----------------------------------------------------------------");
            Console.Write("--->");
            return Console.ReadLine();
        }
        protected override bool Choice(ref string Parametres, string MenuItem)  // обработка выбора пункта меню, возвращает true - если надо завершить работу
        {
            switch (MenuItem)
            {
                case "0":
                    return true;
                case "1":
                    Parametres = "1";
                    break;
                case "2":
                    Parametres = "2";
                    break;
                case "3":
                    Parametres = "3";
                    break;
                case "4":
                    Parametres = "4";
                    break;
                default:
                    Parametres = "";
                    break;
            }
            return false;
        }
        protected override void Message(ref string Parametres) { } // сообщение об ошибке или об успешном выполнении

    }

    public class AddReferenceItems : Window
    {

        protected override void Head()    // шапка
        {
            Console.WriteLine("*****************************************************************");
            Console.WriteLine("Вы находитесь в окне добавления элементов справочников");
            Console.WriteLine();
        }
        protected override void Outputs() { }// вывод отчетов в окне
        protected override void Inputs(ref string Parametres)  // ввод данных
        {
            List<string> Items;   // список элементов справочника для вывода на экран
            string Item = "";     // Новый элемент

            // надписи
            string HeadLine = "";
            string ReferenceName = "";
            string InvitationAdd = "";
            string InvitationDel = "";

            switch (Parametres)
            {
                case "1":
                    HeadLine = "Доступные кошельки:";
                    ReferenceName = "Wallets";
                    InvitationAdd = "Новый кошелек";
                    InvitationDel = "Имя удаляемого кошелька";
                    break;
                case "2":
                    HeadLine = "Доступные статьи расходов: ";
                    ReferenceName = "Expenses";
                    InvitationAdd = "Новая статья расходов";
                    InvitationDel = "Удаляемая статья расходов";
                    break;
                case "3":
                    HeadLine = "Доступные статьи доходов";
                    ReferenceName = "Incomes";
                    InvitationAdd = "Новая статья доходов";
                    InvitationDel = "Удаляемая статья доходов";
                    break;
                default:
                    return;
            }

            do
            {
                Console.Clear();
                Reference Ref = new Reference(ReferenceName);

                // выводим список кошельков
                Console.WriteLine("-----------------------------------------------------------------");
                Console.WriteLine(HeadLine);
                Console.WriteLine();
                try{
                    Items = Ref.GetNamesList();
                }
                catch (Exception e){
                    Console.WriteLine();
                    Console.WriteLine("ERROR: Во время получения списка элементов справочника произошла ошибка: " + e.Message + "\nНажмите Enter для продложения.");
                    Console.WriteLine();
                    Console.ReadLine();
                    continue;
                }
                
                ReportOutput(Items);

                Console.WriteLine("Возврат - 0; добавить элемент - 1; удалить элемент - 2");
                Console.Write("--->");
                string Action = Console.ReadLine();
                if ((Action == "0") || ((Action != "1")&&(Action != "2")) )   // завершаем, если выбран либо 0, либо любой другой символ
                    break;
                
                if (Action == "1")
                    Console.Write(InvitationAdd + " ----->");
                else
                    Console.Write(InvitationDel + " ----->");
                Item = Console.ReadLine();

                try
                {
                    if (Action == "1")
                    {
                        Ref.AddItem(Item);
                        Console.WriteLine();
                        Console.WriteLine("Элемент справочника успешно добавлен! Нажмите Enter для продложения.");
                    }
                    else
                    {
                        Ref.DeleteItem(Item);
                        Console.WriteLine();
                        Console.WriteLine("Элемент справочника успешно удалён! Нажмите Enter для продложения.");
                    }                    
                    Console.WriteLine();
                    Console.ReadLine();
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                    Console.WriteLine("ERROR: Во время добавления/удаления элемента справочника произошла ошибка: " + e.Message + "\nНажмите Enter для продложения.");
                    Console.WriteLine();
                    Console.ReadLine();
                    continue;
                }

                Console.WriteLine("Продолжить ввод? Нет - 0; да - любая клавиша");
                Console.Write("--->");
                if (Console.ReadLine() == "0")
                    break;

            } while (true);
            
        }
        protected override string Menu()  // вывод меню
        {
            Console.WriteLine("-----------------------------------------------------------------");
            Console.WriteLine("Список доступных команд:");
            Console.WriteLine("0 - Выход; 1 - Добавление кошельков; 2 - Добавление статей расходов; 3 - Добавление статей доходов ");
            Console.WriteLine("-----------------------------------------------------------------");
            Console.Write("--->");
            return Console.ReadLine();
        }
        protected override bool Choice(ref string Parametres, string MenuItem)  // обработка выбора пункта меню, возвращает true - если надо завершить работу
        {
            switch (MenuItem)
            {
                case "0":
                    return true;
                case "1":
                    Parametres = "1";
                    break;
                case "2":
                    Parametres = "2";
                    break;
                case "3":
                    Parametres = "3";
                    break;
                default:
                    Parametres = "";
                    break;
            }
            return false;
        }
        protected override void Message(ref string Parametres) { } // сообщение об ошибке или об успешном выполнении
    }

    public class OperationsList : Window
    {

        protected override void Head()    // шапка
        {
            Console.WriteLine("*****************************************************************");
            Console.WriteLine("Вы находитесь в окне просмотра списка операций");
            Console.WriteLine();
        }
        protected override void Outputs() // вывод отчетов в окне
        {
            int CurrentMonth = DateTime.Today.Month;
            int CurrentYear = DateTime.Today.Year;
            int NumberOfDaysInMonth = DateTime.DaysInMonth(CurrentYear, CurrentMonth);
            DateTime BeginMonth = new DateTime(CurrentYear, CurrentMonth, 1);
            DateTime EndMonth = new DateTime(CurrentYear, CurrentMonth, NumberOfDaysInMonth);

            Console.WriteLine("Список операций за период с {0} по {1}", BeginMonth.Date, EndMonth.Date);
            Console.WriteLine();
            ReportOperationsList(BeginMonth, EndMonth);
        }
        protected override void Inputs(ref string Parametres)  // ввод данных
        {
            DateTime StartDate, EndDate;
            switch (Parametres)
            {
                case "1":
                    Console.WriteLine("Введите даты начала и конца периода, за который выводить список операций");
                    Console.Write("Дата начала --->");

                    try
                    {
                        StartDate = DateTime.Parse(Console.ReadLine());
                    }
                    catch (FormatException e)
                    {
                        Console.WriteLine("Недопустимый формат даты. Системное сообщение: " + e.Message);
                        Console.WriteLine("Нажмите Enter для продложения.");
                        Console.ReadLine();
                        break;
                    }


                    Console.Write("Дата конца ---->");
                    try
                    {
                        EndDate = DateTime.Parse(Console.ReadLine());
                    }
                    catch (FormatException e)
                    {
                        Console.WriteLine("Недопустимый формат даты. Системное сообщение: " + e.Message);
                        Console.WriteLine("Нажмите Enter для продложения.");
                        Console.ReadLine();
                        break;
                    }
                    ReportOperationsList(StartDate, EndDate);
                    Console.WriteLine("Нажмите Enter для продолжения");
                    Console.ReadLine();
                    break;
                case "2":
                    Console.WriteLine("");
                    Console.WriteLine("Введите ID операции, которую нужно удалить");
                    Console.Write("ID ----->");

                    int ID;
                    try
                    {
                        ID = int.Parse(Console.ReadLine());
                    }
                    catch (FormatException e)
                    {
                        Console.WriteLine("Недопустимый формат числа. Системное сообщение: " + e.Message);
                        Console.WriteLine("Нажмите Enter для продложения.");
                        Console.ReadLine();
                        break;
                    }
                    try
                    {
                        Console.WriteLine("Операция удалена успешно. Нажмите Enter");
                        Console.ReadLine();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error: " + e.Message);
                        Console.WriteLine("Во время выполнения произошла ошибка! Нажмите Enter");
                        Console.ReadLine();
                    }
                    
                    break;
            }
        }
        protected override string Menu()  // вывод меню
        {
            Console.WriteLine("-----------------------------------------------------------------");
            Console.WriteLine("Список доступных команд:");
            Console.WriteLine("0 - Выход; 1 - Задать другой период; 2 - Удалить операцию ");
            Console.WriteLine("-----------------------------------------------------------------");
            Console.Write("--->");
            return Console.ReadLine();
        }
        protected override bool Choice(ref string Parametres, string MenuItem)  // обработка выбора пункта меню, возвращает true - если надо завершить работу
        {
            switch (MenuItem)
            {
                case "0":
                    return true;
                case "1":
                    Parametres = "1";
                    break;
                case "2":
                    Parametres = "2";
                    break;
                default:
                    Console.WriteLine("Введена недопустимая команда!");
                    break;
            }
            return false;
        }
        protected override void Message(ref string Parametres) { } // сообщение об ошибке или об успешном выполнении
    }

}