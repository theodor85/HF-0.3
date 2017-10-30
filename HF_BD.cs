using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
using System.Globalization;

namespace HomeFinance.AccountingSystem
{

    // типы операций: расход, доход, перемещение, ввод остатка
    public enum OperationTypes : byte
    {
        Expense = 1,
        Income = 2,
        Transfer = 3,
        Rest = 0
    }

    // перечисление - вид операции для доступа к таблицам базы данных
    public enum QueryType
    {
        INSERT = 1,
        SELECT = 2,
        UPDATE = 3,
        DELETE = 4
    }

    #region//****************** Блок публичных структур для отчетов ************************** 

    // Структура, описывающая состояние элемента справочнка. Под состоянием понимается пара 
    // <ИмяЭлемента, Сумма>. 
    // "ИмяЭлемента" - имя жлемента справочнка расходов, доходов или кошельков
    // "Сумма" - значение расхода, дохода или остаток в кошельке соответственно
    public struct TItemState
    {
        public string ItemName;
        public double Sum;
    }

    // описание операции
    public struct TOperation
    {
        public DateTime Date;
        public int ID;
        public OperationTypes OperationType;
        public string Source;
        public string Destination;
        public double Sum;
    }

    #endregion

    //********************* Абстрактный объект для всех компонентов систумы учета(справочников, операций, регистров...) 
    public abstract class AccountingSystemObject
    {
        protected static LowLevelAccessProvider DataBaseAccessProvider;
        protected void Initialization()
        {
            if (DataBaseAccessProvider == null)
                DataBaseAccessProvider = new LowLevelAccessProvider();
        }

    } 

    //********************* Класс Cправочник **********************************************
    public class Reference: AccountingSystemObject
    {
        string ReferenceName;

        public Reference(string ReferenceName)
        {
            this.ReferenceName = ReferenceName;
            base.Initialization();
        }
        public void AddItem(string ItemName)
        {
            // синтезируем строку SQL-запроса и обращаемся к БД
            string CommandString = "INSERT INTO HomeFinance.dbo." + ReferenceName + " (Name) VALUES (N\'" + ItemName + "\')";
            try {
                DataBaseAccessProvider.ChangeDataInBD(CommandString);
            }
            catch (Exception e){
                throw new Exception("Ошибка в функции Reference.AddItem: ошибка доступа к БД. Сообщение: " + e.Message, e);
            }
        }
        public List<string> GetItemsList()
        {            
            List<string> ItemsList = new List<string>(); // выходной список
            DbDataReader dr;
            string CommandString = "SELECT * FROM HomeFinance.dbo." + ReferenceName;
            
            // обращаемся к БД и получаем объект чтения
            try{
                dr = DataBaseAccessProvider.ReadDataFromBD(CommandString);
            }
            catch (Exception e){
                throw new Exception("Ошибка в функции Reference.GetItemsList. Ошибка доступа к БД. Сообщение: " + e.Message, e);
            }

            // читаем записи из объекта чтения и добавляем полученный элемент в список
            while (dr.Read()){ 
                ItemsList.Add(dr["Name"].ToString());
            }
            return ItemsList;
        }
        public void DeleteItem(string ItemName)
        {
            // синтезируем строку SQL-запроса и обращаемся к БД
            string CommandString = "DELETE FROM HomeFinance.dbo." + ReferenceName + " WHERE Name = N\'" + ItemName + "\'";
            try{
                DataBaseAccessProvider.ChangeDataInBD(CommandString);
            }
            catch (Exception e){
                throw new Exception("Ошибка в функции Reference.AddItem: ошибка доступа к БД. Сообщение: " + e.Message, e);
            }
        }
    }

    //********************* Публичный класс регистра оборотов - для получения отчетов по регистру ****************************
    public class RegisterTurnoverReports:AccountingSystemObject
    {
        string RegisterName;
        public RegisterTurnoverReports(string RegisterName)
        {
            this.RegisterName = RegisterName;
            base.Initialization();
        }
        public List<TItemState> ReportTurnover(DateTime BeginDate, DateTime EndDate)
        {            
            List<TItemState> TurnoversList = new List<TItemState>();  // результирующий список            
            string CommandString = "";                                     // строка SQL- запроса
            DbDataReader DReader;                                     // объект чтения данных
            // строковое представление даты для SQL-запроса
            string strBeginDate = Utility.MakeSQLDate(BeginDate) ;
            string strEndDate = Utility.MakeSQLDate(EndDate);

            // получим все статьи расходов из справочника
            CommandString = "SELECT ID, Name FROM HomeFinance.dbo." + RegisterName;
            try{
                DReader = DataBaseAccessProvider.ReadDataFromBD(CommandString);
            }
            catch (Exception e){
                throw new Exception("Функция RegisterTurnoverReports.ReportTurnover: ошибка доступа к справочнику статей расходов. Сообщение: " + e.Message, e);
            }

            CommandString = "SELECT E.Name as Expense, SUM(R.MoveSum) as Rez FROM HomeFinance.dbo.RT_Moves_" + RegisterName;
            CommandString = CommandString + " R, HomeFinance.dbo." + RegisterName + " E, HomeFinance.dbo.Operations O ";
            CommandString = CommandString + "WHERE E.ID = R.Name AND O.ID = R.Operation AND O.Date >=" + strBeginDate + " AND O.Date <=" + strEndDate;
            CommandString = CommandString + "group by E.Name ";

            try{
                DReader = DataBaseAccessProvider.ReadDataFromBD(CommandString);
            }
            catch (Exception e){
                throw new Exception("Функция RegisterTurnoverReports.ReportTurnover: ошибка получения информации из регистра расходов. Сообщение: " + e.Message, e);
            }

            while (DReader.Read())
            {
                TItemState NewItem = new TItemState();
                NewItem.ItemName = DReader["Expense"].ToString();
                NewItem.Sum = (double)DReader["Rez"];
                TurnoversList.Add(NewItem);
            }
            return TurnoversList;
        }
    }

    //********************* Приватный класс регистра оборотов - для выполнения движений по регистру **************************

    //********************* Публичный класс регистра остатков - для получения отчетов по регистру ****************************
    public class RegisterRestReports : AccountingSystemObject
    {
        string RegisterName;
        public RegisterRestReports(string RegisterName)
        {
            this.RegisterName = RegisterName;
            base.Initialization();
        }
        public List<TItemState> ReportRests(DateTime Date)
        {
            
            List<TItemState> BalanceList = new List<TItemState>(); // результирующий список             
            string CommandString;// строка SQL- запроса
            string strDate = Utility.MakeSQLDate(Date);  // строковое представление даты
            List<string> ReferenceItems = new List<string>();  // список элементов справочника, ассоциированного с регистром

            // получим все элементы из справочника
            Reference Ref = new Reference(RegisterName);
            ReferenceItems = Ref.GetItemsList();

            CommandString = "";
            try
            {
                foreach (string item in ReferenceItems)
                {
                    CommandString = "SELECT top(1) R.Date as Dt, W.Name as Nm, R.Rest as Sum FROM HomeFinance.dbo.RR_Rests_Wallets R, HomeFinance.dbo.Wallets W ";
                    CommandString = CommandString + "WHERE R.Date<=" + strDate + " AND R.Name = W.ID AND W.Name = N\'" + item + "\'";
                    CommandString = CommandString + " Order by Dt desc";

                    DbDataReader Dr = DataBaseAccessProvider.ReadDataFromBD(CommandString);

                    if (Dr.Read())
                    {
                        TItemState NewItem;
                        NewItem.ItemName = Dr["Nm"].ToString();
                        NewItem.Sum = (double)Dr["Sum"];
                        BalanceList.Add(NewItem);
                    }
                }
            }
            catch (Exception e){
                throw new Exception("Функция ReportBalance: ошибка получения отчета об остaтках. Сообщение: " + e.Message, e);
            }

            return BalanceList;
        }
    }

    //********************* Приватный класс регистра остатков - для выполнения движений по регистру **************************

    public class HF_BD
    {
        // поля для инициализации базы данных DataProvider и ConnString
        string DataProvider;
        string ConnString;

        // фабрика поставщиков и подключение
        DbProviderFactory ProviderFactory;
        DbConnection Connection;

        // номер (ID) последней операции
        int CurrentOperationID;

        // конструктор
        public HF_BD()
        {
            // получаем провайдер данных и строку подклчения из конфигурационного файла
            DataProvider = ConfigurationManager.AppSettings["provider"];
            ConnString = ConfigurationManager.AppSettings["cnStr"];

            // получаем фабрику поставщиков и объект подключения
            ProviderFactory = DbProviderFactories.GetFactory(DataProvider);
            Connection = ProviderFactory.CreateConnection();
            Connection.ConnectionString = ConnString;

            // получаем текущий номер последней операции
            CurrentOperationID = GetCurrentOperationID();
        }

        #region//****************** ПУБЛИЧНЫЕ ФУНКЦИИ **************************
        //

        // добавление элемента справочнка
        // возващаемые значения:
        public void AddReferenceItem(string ReferenceName, string ItemName)
        {
            // синтезируем строку SQL-запроса
            string CommandString = "INSERT INTO HomeFinance.dbo." + ReferenceName + " (Name) VALUES (N\'" + ItemName + "\')";
            // обращаемся к БД
            try
            {
                ChangeDataInBD(CommandString);
            }
            catch (Exception e)
            {
                throw new Exception("Ошибка в функции AddReferenceItem: ошибка доступа к БД. Сообщение: " + e.Message, e);
            }
            
        }

        // получение списка элементов справочника
        public List<string> GetReferenceItemsList(string ReferenceName)
        {
            // выходной список
            List<string> ItemsList = new List<string>();
            DbDataReader dr;

            // синтезируем строку SQL-запроса
            string CommandString = "SELECT * FROM HomeFinance.dbo." + ReferenceName;
            // обращаемся к БД и получаем объект чтения
            try
            {
                dr = ReadDataFromBD(CommandString);
            }
            catch (Exception e)
            {
                throw new Exception("ОШибка в функции GetReferenceItemsList. Ошибка доступа к БД. Сообщение: " + e.Message, e);
            }
            
            // читаем записи из объекта чтения
            while (dr.Read())
            {
                // добавляем полученный элемент в список
                ItemsList.Add(dr["Name"].ToString());
            }
            return ItemsList;
        }

        // ввод операции
        public void EnterOperation(OperationTypes TypeOfTheOperation, DateTime DateOfTheOperation, string Source, string Destination, double Sum)
        {
            // переменные
            DbCommand cmd = ProviderFactory.CreateCommand();  // объект команды для записи в БД
            string CommandString = ""; // текст результирующего SQL- запроса
            int IntTypeOfTheOperation = (int)TypeOfTheOperation;  // получим тип операции в виде целого числа, для записи в таблицу БД 
            string strDateOfTheOperation = "'" + DateOfTheOperation.Month.ToString() + "." + DateOfTheOperation.Day.ToString() + "." + DateOfTheOperation.Year.ToString() + "'";
            DbTransaction Transaction;   // объект транзакции
            int IDOfTheOperation = CurrentOperationID + 1;  // номер нашей операции будет на 1 больше, чем номер последней

            if (Source == "")
                Source = "_";
            if (Destination == "")
                Destination = "_";

            // формируем SQL-запрос в переменной CommandString
            // записываем операцию в таблицу Operations
            CommandString = "INSERT INTO HomeFinance.dbo.Operations (ID, Date, OperationType, Source, Destination, Sum)";
            CommandString = CommandString + " VALUES (" + IDOfTheOperation.ToString() + ", " + strDateOfTheOperation + ", " 
                + IntTypeOfTheOperation.ToString() + ", N\'" + Source + "\', N\'" + Destination + "\', " + Sum.ToString("#####.00", CultureInfo.CreateSpecificCulture("en-US")) +"); ";
        //    N\'" + ItemName + "\'
            // движения регистров, в зависимости от типа операции

            switch (TypeOfTheOperation)
            {
                case OperationTypes.Rest:     // операция введения остатков
                    RR_Moving(ref CommandString, "Wallets", IDOfTheOperation, DateOfTheOperation, Destination, Sum );  // движение по регистру остатков
                    break;
                case OperationTypes.Expense:   //операция добавления расходов
                    RR_Moving(ref CommandString, "Wallets", IDOfTheOperation, DateOfTheOperation, Source, -Sum);   // движение по регистру остатков (списание средств)
                    RT_Moving(ref CommandString, "Expenses", IDOfTheOperation, DateOfTheOperation, Destination, Sum);    // движение по регистру оборотов (расходы)
                    break;
                case OperationTypes.Income:
                    RR_Moving(ref CommandString, "Wallets", IDOfTheOperation, DateOfTheOperation, Destination, Sum);   // движение по регистру остатков 
                    RT_Moving(ref CommandString, "Incomes", IDOfTheOperation, DateOfTheOperation, Source, Sum);    // движение по регистру оборотов
                    break;
                case OperationTypes.Transfer:
                    RR_Moving(ref CommandString, "Wallets", IDOfTheOperation, DateOfTheOperation, Source, -Sum);   // движение по регистру остатков 
                    RR_Moving(ref CommandString, "Wallets", IDOfTheOperation, DateOfTheOperation, Destination, Sum);   // движение по регистру остатков 
                    break;
            }

            // начинаем транзакцию
            Connection.Open();
            Transaction = Connection.BeginTransaction();
            cmd.Connection = Connection;
            cmd.CommandText = CommandString;
            cmd.Transaction = Transaction;
            
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch(DbException e)
            {
                Transaction.Rollback();
                Connection.Close();
                throw new Exception("При вводе операции произошла ошибка доступа к базе данных! " + e.Message, e);
            }
            Transaction.Commit();
            Connection.Close();
            CurrentOperationID = IDOfTheOperation;   // т.к. операция записалась успешно, то теперь максимальный номер операции
            // будет равен номеру той операции, которую только что записали

        }

        // удаление операции
        public void DeleteOperation(int OperationID)
        {
            // переменные
            DbCommand cmd = ProviderFactory.CreateCommand();  // объект команды для записи в БД
            DbTransaction Transaction;   // объект транзакции
            string CommandString = ""; // текст результирующего SQL- запроса
            DbDataReader DReader;

            int IntTypeOfTheOperation;  // тип операции получим из запроса
            OperationTypes TypeOfTheOperation;
            string strOperationDate; // строковое представление даты удаляемой операции 
            DateTime OperationDate;  // дата удаляемой операции
            string Source;           // Разрезы учета удаляемых операций  
            string Destination;
            double Sum;               // сумма удаляемой операции

            // получим данные о нашей операции
            string cstr = "SELECT * FROM HomeFinance.dbo.Operations WHERE ID = " + OperationID.ToString();
            try
            {
                DReader = ReadDataFromBD(cstr);
            }
            catch (DbException e)
            {
                throw new Exception("DeleteOperation: Произошла ошибка при чтении из таблицы операций ", e);
            }

            if (DReader.Read())
            {
                try
                {
                    OperationDate = DateTime.Parse(DReader["Date"].ToString());
                    IntTypeOfTheOperation = int.Parse(DReader["OperationType"].ToString());
                    TypeOfTheOperation = (OperationTypes)IntTypeOfTheOperation;
                    Source = DReader["Source"].ToString();
                    Destination = DReader["Destination"].ToString();
                    Sum = double.Parse(DReader["Sum"].ToString());
                }
                catch (Exception e)
                {
                    throw new Exception("DeleteOperation: ошибка при получении данных операции с ID=" + OperationID.ToString(), e);
                }
                
            }
            else
                throw new Exception("DeleteOperation: Не найдена операция с ID=" + OperationID.ToString());
            strOperationDate = "'" + OperationDate.Month.ToString() + "." + OperationDate.Day.ToString() + "." + OperationDate.Year.ToString() + "'";
            

            // удаляем движения регистров в зависимости от типа операции
            switch (TypeOfTheOperation)
            {
                case OperationTypes.Rest:
                    RR_Delete_Moving(ref CommandString, "Wallets", OperationID, OperationDate, Destination, Sum);
                    break;
                case OperationTypes.Expense:   
                    RR_Delete_Moving(ref CommandString, "Wallets", OperationID, OperationDate, Source, -Sum);   
                    RT_Delete_Moving(ref CommandString, "Expenses", OperationID); 
                    break;
                case OperationTypes.Income:
                    RR_Delete_Moving(ref CommandString, "Wallets", OperationID, OperationDate, Destination, Sum); 
                    RT_Delete_Moving(ref CommandString, "Incomes", OperationID);
                    break;
                case OperationTypes.Transfer:
                    RR_Delete_Moving(ref CommandString, "Wallets", OperationID, OperationDate, Source, -Sum);   
                    RR_Delete_Moving(ref CommandString, "Wallets", OperationID, OperationDate, Destination, Sum);
                    break;
            }

            // удаляем саму операцию
            CommandString = CommandString + "DELETE FROM HomeFinance.dbo.Operations WHERE ID = " + OperationID.ToString();

            // начинаем транзакцию
            Connection.Open();
            Transaction = Connection.BeginTransaction();
            cmd.Connection = Connection;
            cmd.CommandText = CommandString;
            cmd.Transaction = Transaction;

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (DbException e)
            {
                Transaction.Rollback();
                Connection.Close();
                throw new Exception("DeleteOperation: При вводе операции произошла ошибка доступа к базе данных! " + e.Message, e);
            }
            Transaction.Commit();
            Connection.Close();
        }

        // Отчет Остатки в кошельках на заданную дату
        public List<TItemState> ReportBalance(DateTime DT)
        {
            // результирующий список
            List<TItemState> BalanceList = new List<TItemState>();
            // строка SQL- запроса 
            string CommandString;
            // объект чтения данных
            DbDataReader DReader;
            // строковое представление даты
            string strDate = "'" + DT.Month.ToString() + "." + DT.Day.ToString() + "." + DT.Year.ToString() + "'";

            // получим все кошельки изсправочника
            CommandString = "SELECT ID, Name FROM HomeFinance.dbo.Wallets";
            try
            {
                DReader = ReadDataFromBD(CommandString);
            }
            catch (Exception e)
            {
                throw new Exception("Функция ReportBalance: ошибка доступа к таблице кошельков. Сообщение: " + e.Message, e);
            }

            CommandString = "";
            try
            {
                while (DReader.Read())
                {
                    string strID = DReader["ID"].ToString();

                    CommandString = "SELECT top(1) R.Date as Dt, W.Name as Nm, R.Rest as Sum FROM HomeFinance.dbo.RR_Rests_Wallets R, HomeFinance.dbo.Wallets W ";
                    CommandString = CommandString + "WHERE R.Date<=" + strDate + " AND R.Name = W.ID AND R.Name = " + strID;
                    CommandString = CommandString + " Order by Dt desc";

                    DbDataReader Dr = ReadDataFromBD(CommandString);

                    if (Dr.Read())
                    {
                        TItemState NewItem;
                        NewItem.ItemName = Dr["Nm"].ToString();
                        NewItem.Sum = (double)Dr["Sum"];
                        BalanceList.Add(NewItem);
                    }
                }
            }
            catch (Exception e)
            {

                throw new Exception("Функция ReportBalance: ошибка получения отчета об осттках. Сообщение: " + e.Message, e);
            }
            
            return BalanceList;
        }

        // Отчет Расходы за период
        public List<TItemState> ReportExpenses(DateTime DT1, DateTime DT2)
        {
            // результирующий список
            List<TItemState> ExpensesList = new List<TItemState>();
            // строка SQL- запроса 
            string CommandString;
            // объект чтения данных
            DbDataReader DReader;
            // строковое представление даты
            string strBeginDate = "'" + DT1.Month.ToString() + "." + DT1.Day.ToString() + "." + DT1.Year.ToString() + "'";
            string strEndDate   = "'" + DT2.Month.ToString() + "." + DT2.Day.ToString() + "." + DT2.Year.ToString() + "'";

            // получим все статьи расходов из справочника
            CommandString = "SELECT ID, Name FROM HomeFinance.dbo.Expenses";
            try
            {
                DReader = ReadDataFromBD(CommandString);
            }
            catch (Exception e)
            {
                throw new Exception("Функция ReportExpenses: ошибка доступа к справочнику статей расходов. Сообщение: " + e.Message, e);
            }

            CommandString = "";
            CommandString = "SELECT E.Name as Expense, SUM(R.MoveSum) as Rez FROM HomeFinance.dbo.RT_Moves_Expenses R, HomeFinance.dbo.Expenses E, HomeFinance.dbo.Operations O ";
            CommandString = CommandString + "WHERE E.ID = R.Name AND O.ID = R.Operation AND O.Date >=" + strBeginDate + " AND O.Date <=" + strEndDate;
            CommandString = CommandString + "group by E.Name ";

            try
            {
                DReader = ReadDataFromBD(CommandString);
            }
            catch (Exception e)
            {
                throw new Exception("Функция ReportExpenses: ошибка получения информации из регистра расходов. Сообщение: " + e.Message, e);
            }

            while(DReader.Read())
            {
                TItemState NewItem = new TItemState();
                NewItem.ItemName = DReader["Expense"].ToString();
                NewItem.Sum =      (double)DReader["Rez"];
                ExpensesList.Add(NewItem);
            }
            
            return ExpensesList;
        }

        // Отчет Доходы за период
        public List<TItemState> ReportIncomes(DateTime DT1, DateTime DT2)
        {
            // результирующий список
            List<TItemState> ExpensesList = new List<TItemState>();
            // строка SQL- запроса 
            string CommandString;
            // объект чтения данных
            DbDataReader DReader;
            // строковое представление даты
            string strBeginDate = "'" + DT1.Month.ToString() + "." + DT1.Day.ToString() + "." + DT1.Year.ToString() + "'";
            string strEndDate = "'" + DT2.Month.ToString() + "." + DT2.Day.ToString() + "." + DT2.Year.ToString() + "'";

            // получим все статьи расходов из справочника
            CommandString = "SELECT ID, Name FROM HomeFinance.dbo.Incomes";
            try
            {
                DReader = ReadDataFromBD(CommandString);
            }
            catch (Exception e)
            {
                throw new Exception("Функция ReportExpenses: ошибка доступа к справочнику статей расходов. Сообщение: " + e.Message, e);
            }

            CommandString = "";
            CommandString = "SELECT E.Name as Expense, SUM(R.MoveSum) as Rez FROM HomeFinance.dbo.RT_Moves_Incomes R, HomeFinance.dbo.Incomes E, HomeFinance.dbo.Operations O ";
            CommandString = CommandString + "WHERE E.ID = R.Name AND O.ID = R.Operation AND O.Date >=" + strBeginDate + " AND O.Date <=" + strEndDate;
            CommandString = CommandString + "group by E.Name ";

            try
            {
                DReader = ReadDataFromBD(CommandString);
            }
            catch (Exception e)
            {
                throw new Exception("Функция ReportExpenses: ошибка получения информации из регистра доходов. Сообщение: " + e.Message, e);
            }

            while (DReader.Read())
            {
                TItemState NewItem = new TItemState();
                NewItem.ItemName = DReader["Expense"].ToString();
                NewItem.Sum = (double)DReader["Rez"];
                ExpensesList.Add(NewItem);
            }

            return ExpensesList;
        }

        // Список операций за период
        public List<TOperation> GetOperationsList(DateTime DT1, DateTime DT2)
        {
            // результирующий список
            List<TOperation> OperationsList = new List<TOperation>();
            // строка SQL- запроса 
            string CommandString;
            // объект чтения данных
            DbDataReader DReader;
            // строковое представление даты
            string strBeginDate = "'" + DT1.Month.ToString() + "." + DT1.Day.ToString() + "." + DT1.Year.ToString() + "'";
            string strEndDate = "'" + DT2.Month.ToString() + "." + DT2.Day.ToString() + "." + DT2.Year.ToString() + "'";

            // получим все операции за период
            CommandString = "SELECT * FROM HomeFinance.dbo.Operations ";
            CommandString = CommandString + "WHERE Date>=" + strBeginDate + " AND Date<= " + strEndDate;
            try
            {
                DReader = ReadDataFromBD(CommandString);
            }
            catch (Exception e)
            {
                throw new Exception("Функция GetOperationsList: ошибка доступа к списку операций. Сообщение: " + e.Message, e);
            }

            try
            {
                while (DReader.Read())
                {
                    TOperation NewItem = new TOperation();
                    NewItem.ID = (int)DReader["ID"];
                    NewItem.Date = DateTime.Parse(DReader["Date"].ToString());
                    NewItem.OperationType = (OperationTypes)int.Parse(DReader["OperationType"].ToString());
                    NewItem.Source = DReader["Source"].ToString();
                    NewItem.Destination = DReader["Destination"].ToString();
                    NewItem.Sum = (double)DReader["Sum"];
                    OperationsList.Add(NewItem);
                }
            }
            catch (Exception e)
            {

                throw new Exception("Функция GetOperationsList: ошибка преобразовании значений. Сообщение: ", e);
            }
            

            return OperationsList;
        }

        #endregion

        #region //****************** ФУНКЦИИ РАБОТЫ С БАЗОЙ ДАННЫХ **************************
        //

        // функция чтения данных из БД
        // возвращает объект чтения данных.
        DbDataReader ReadDataFromBD(string CommandString)
        {
            // получаем фабрику поставщиков
            DbProviderFactory df = DbProviderFactories.GetFactory(DataProvider);
            // работа с подключением 
            DbConnection cn = df.CreateConnection();

            cn.ConnectionString = ConnString;
            cn.Open();
            // объект команды
            DbCommand cmd = df.CreateCommand();
            cmd.Connection = cn;
            cmd.CommandText = CommandString;

            // используем объект чтения
            DbDataReader dr = cmd.ExecuteReader();
            return dr;

        }
        // функция изменения данных в БД
        // возвращает 0, если выполнено без ошибок.
        // -1 Ошибка доступа к БД
        int ChangeDataInBD(string CommandString)
        {
            // получаем фабрику поставщиков
            DbProviderFactory df = DbProviderFactories.GetFactory(DataProvider);
            // работа с подключением 
            using (DbConnection cn = df.CreateConnection())
            {
                cn.ConnectionString = ConnString;
                cn.Open();
                // объект команды
                DbCommand cmd = df.CreateCommand();
                cmd.Connection = cn;
                cmd.CommandText = CommandString;

                // используем объект чтения
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch
                {
                    return -1;
                }
            }
            return 0;
        }
        #endregion

        #region//****************** ФУНКЦИИ ВЫПОЛНЯЮЩИЕ ДВИЖЕНИЯ РЕГИСТРОВ *****************

        //функция формирует SQL-запрос для движения по регистру остатков
        void RR_Moving(ref string CommandString, string RegisterName, int OperationNumber, DateTime OperationDate, string Name, double Sum)
        {
            string strDate = "'" + OperationDate.Month.ToString() + "." + OperationDate.Day.ToString() + "." + OperationDate.Year.ToString() + "'";

            // получить ID заданного элемента
            string csGetID = "SELECT ID FROM HomeFinance.dbo." + RegisterName + " WHERE Name = N\'" + Name + "'";
            DbDataReader DReader;
            try
            {
                DReader = ReadDataFromBD(csGetID);
            }
            catch (DbException e)
            {
                throw new Exception("Произошла ошибка при чтении из справочника " + RegisterName + " элемента " + Name, e);
            }

            // здесь зделать проверку, что такой кошелёк найден
            string ID;
            if (DReader.Read())
                ID = DReader["ID"].ToString();
            else
                throw new Exception("Не найден элемент '" + Name + "' справочника '" + RegisterName + "'");

            // сконструировать строку - SQL-запрос
            CommandString = CommandString + "INSERT INTO HomeFinance.dbo.RR_Moves_" + RegisterName + " (Operation, Name, MoveSum)";
            CommandString = CommandString + " VALUES (" + OperationNumber.ToString() + ", " + ID + ", " + Sum.ToString("#####.00", CultureInfo.CreateSpecificCulture("en-US")) + "); ";
            

            // пересчитать таблицу остатков
            // 

            /* делаем запрос, в качестве условия - меньше либо равно текщец дате
             * при этом сортируем по дате по убыванию. Таким образом, сама поздняя дата окажется первой в результатах запроса
             * если же запрос не вернет строк, это значит, либо таблица пуста, либо нет движений регистра раннее текущей даты
             * в любом из этих случаев просто добавляем строку с текущей датой и суммой. Далее проверяем, нет ли строк сдатой
             * большей, чем текущая. Тогда эти сроки надо все апдейтить на сумму нашей операции.
             * 
             */
            //сконструировать строку - SQL-запрос
            string csGetDate = "SELECT Date, Rest FROM HomeFinance.dbo.RR_Rests_" + RegisterName + " WHERE Date<=" + strDate + " and Name=" + ID + " Order by Date desc";
            try
            {
                DReader = ReadDataFromBD(csGetDate);
            }
            catch (Exception e)
            {
                throw new Exception("Произошла ошибка при запросе из таблицы остатков регистра остатков " + RegisterName, e);
            }
            
            // есть ли строки в выборке?
            if (DReader.HasRows)   // есть
            {
                DReader.Read();
                // "Верхняя" дата равна текущей?
                if ((DateTime)DReader["Date"] == OperationDate)    // да
                {
                    CommandString = CommandString + "UPDATE HomeFinance.dbo.RR_Rests_" + RegisterName + " SET Rest = Rest + " + Sum.ToString("#####.00", CultureInfo.CreateSpecificCulture("en-US")) + " WHERE Date = " + strDate + " and Name=" + ID + "; ";
                }
                else      // нет
                {
                    double AddSum;
                    AddSum = Sum + double.Parse(DReader["Rest"].ToString());

                    CommandString = CommandString + "INSERT INTO HomeFinance.dbo.RR_Rests_" + RegisterName + " (Date, Name, Rest) ";
                    CommandString = CommandString + "VALUES (" + strDate + ", " + ID + ", " + AddSum.ToString("#####.00", CultureInfo.CreateSpecificCulture("en-US")) + "); ";
                }

            }
            else   //нет строк в выборке
            {
                CommandString = CommandString + "INSERT INTO HomeFinance.dbo.RR_Rests_" + RegisterName + " (Date, Name, Rest) ";
                CommandString = CommandString + "VALUES (" + strDate + ", " + ID + ", " + Sum.ToString("#####.00", CultureInfo.CreateSpecificCulture("en-US")) + "); ";
            }

            // есть ли строки с датой большей даты операции ?
            // увеличиваем остатки во всех строках с датой большей даты текущей операции (если такие строки есть) на сумму текущей операции
            CommandString = CommandString + "UPDATE HomeFinance.dbo.RR_Rests_" + RegisterName + " SET Rest = Rest + " + Sum.ToString("#####.00", CultureInfo.CreateSpecificCulture("en-US")) + " WHERE Date > " + strDate + " and Name=" + ID + "; ";

        }

        //функция формирует SQL-запрос для движения по регистру оборотов
        void RT_Moving(ref string CommandString, string RegisterName, int OperationNumber, DateTime OperationDate, string Dimension, double Sum)
        {
       
            string strDate = "'" + OperationDate.Month.ToString() + "." + OperationDate.Day.ToString() + "." + OperationDate.Year.ToString() + "'";
            string strID;  // ID элемента справочника (разеза учета)
            DbDataReader DReader;

            // получить ID нашего разреза учета 
            string csGetID = "SELECT ID FROM HomeFinance.dbo." + RegisterName + " WHERE Name = N\'" + Dimension + "'";
            try
            {
                DReader = ReadDataFromBD(csGetID);
            }
            catch (DbException e)
            {
                throw new Exception("Произошла ошибка при чтении из справочника " + RegisterName + " элемента " + Dimension, e);
            }

            // здесь зделать проверку, что такой разрез учета найден
            if (DReader.Read())
                strID = DReader["ID"].ToString();
            else
                throw new Exception("RT_Moving: Не найден элемент '" + Dimension + "' справочника '" + RegisterName + "'");

            // сконструировать строку - SQL-запрос
            CommandString = CommandString + "INSERT INTO HomeFinance.dbo.RT_Moves_" + RegisterName + " (Operation, Name, MoveSum)";
            CommandString = CommandString + " VALUES (" + OperationNumber.ToString() + ", " + strID + ", " + Sum.ToString("#####.00", CultureInfo.CreateSpecificCulture("en-US")) + "); ";

        }

        //функция формирует SQL-запрос для удаления движения по регистру оборотов
        void RT_Delete_Moving(ref string CommandString, string RegisterName, int OperationNumber)
        {
            CommandString = CommandString + "DELETE FROM HomeFinance.dbo.RT_Moves_"+ RegisterName;
            CommandString = CommandString + "WHERE Operation = " + OperationNumber.ToString() + "; ";
        }

        //функция формирует SQL-запрос для удаления движения по регистру остатков
        void RR_Delete_Moving(ref string CommandString, string RegisterName, int OperationNumber, DateTime DateOfOperationDeleted, string Name, double Sum)
        {
            // удаление из ьаблицы движений
            CommandString = CommandString + "DELETE FROM HomeFinance.dbo.RR_Moves_" + RegisterName;
            CommandString = CommandString + " WHERE Operation = " + OperationNumber.ToString() + "; ";

            string strDate = "'" + DateOfOperationDeleted.Month.ToString() + "." + DateOfOperationDeleted.Day.ToString() + "." + DateOfOperationDeleted.Year.ToString() + "'";
            string ID;  // ID разреза учета, по которому удаляется движение

            // получить ID заданного элемента
            string csGetID = "SELECT ID FROM HomeFinance.dbo." + RegisterName + " WHERE Name = N\'" + Name + "'";
            DbDataReader DReader;
            try
            {
                DReader = ReadDataFromBD(csGetID);
            }
            catch (DbException e)
            {
                throw new Exception("RR_Delete_Moving: Произошла ошибка при чтении из справочника " + RegisterName + " элемента " + Name, e);
            }

            // здесь зделать проверку, что такой кошелёк найден

            if (DReader.Read())
                ID = DReader["ID"].ToString();
            else
                throw new Exception("RR_Delete_Moving: Не найден элемент '" + Name + "' справочника '" + RegisterName + "'");

            // пересчет таблицы остатков
            // есть ли строки с датой большей даты операции ?
            // увеличиваем остатки во всех строках с датой большей даты текущей операции (если такие строки есть) на сумму текущей операции
            CommandString = CommandString + "UPDATE HomeFinance.dbo.RR_Rests_" + RegisterName + " SET Rest = Rest - " + Sum.ToString("#####.00", CultureInfo.CreateSpecificCulture("en-US")) + " WHERE Date >= " + strDate + " and Name=" + ID + "; ";

        }

        #endregion

        #region//****************** ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ********************************

        // получает из БД номер последней операции во время инициализации БД
        int GetCurrentOperationID()
        {
            // находим максимальный номер OperationID
            string CommandString = "select max(ID) as maxOpID from HomeFinance.dbo.Operations";
            DbDataReader dr;  // Объект чтения данных
            int MaxOperationID; // здесь будет результат

            try
            {
                dr = ReadDataFromBD(CommandString);
            }
            catch(Exception e)
            {
                throw new Exception("Ошибка получения данных о номере операции. БД не инициализирована!", e);
            }

            dr.Read();
            if (dr.IsDBNull(0))
                MaxOperationID = -1;
            else
                MaxOperationID = (int)dr["maxOpID"];
            return MaxOperationID;
        }
        #endregion
    }

    // ***************************** Класс, абстрагирующий низкоуровневые операции с Базой данных *****
    public class LowLevelAccessProvider
    {

        // поля для инициализации базы данных DataProvider и ConnString
        string DataProvider;
        string ConnString;

        // фабрика поставщиков и подключение
        DbProviderFactory ProviderFactory;
        DbConnection Connection;

        int CurrentOperationID;

        // конструктор
        public LowLevelAccessProvider()
        {
            // получаем провайдер данных и строку подклчения из конфигурационного файла
            DataProvider = ConfigurationManager.AppSettings["provider"];
            ConnString = ConfigurationManager.AppSettings["cnStr"];

            // получаем фабрику поставщиков и объект подключения
            ProviderFactory = DbProviderFactories.GetFactory(DataProvider);
            Connection = ProviderFactory.CreateConnection();
            Connection.ConnectionString = ConnString;

            // получаем текущий номер последней операции
            CurrentOperationID = ExtractCurrentOperationID();
        }

        int ExtractCurrentOperationID()
        {
            // находим максимальный номер OperationID
            string CommandString = "select max(ID) as maxOpID from HomeFinance.dbo.Operations";
            DbDataReader dr;  // Объект чтения данных
            int MaxOperationID; // здесь будет результат

            try{
                dr = ReadDataFromBD(CommandString);
            }
            catch (Exception e){
                throw new Exception("LowLevelAccessProvider.GetCurrentOperationID: Ошибка получения данных о номере операции. БД не инициализирована! Сообщение: " + e.Message, e);
            }

            dr.Read();
            if (dr.IsDBNull(0))
                MaxOperationID = -1;
            else
                MaxOperationID = (int)dr["maxOpID"];
            return MaxOperationID;
        }

        public int GetCurrentOperationID()
        {
            return CurrentOperationID;
        }
        public void IncreaseCurrentOperationID()
        {
            CurrentOperationID++;
        }

        // функция чтения данных из БД
        public DbDataReader ReadDataFromBD(string CommandString)
        {
            // получаем фабрику поставщиков
            DbProviderFactory df = DbProviderFactories.GetFactory(DataProvider);
            // работа с подключением 
            DbConnection cn = df.CreateConnection();

            cn.ConnectionString = ConnString;
            cn.Open();
            // объект команды
            DbCommand cmd = df.CreateCommand();
            cmd.Connection = cn;
            cmd.CommandText = CommandString;

            // используем объект чтения
            DbDataReader dr = cmd.ExecuteReader();
            return dr;

        }
        // функция изменения данных в БД
        public void ChangeDataInBD(string CommandString)
        {
            // получаем фабрику поставщиков
            DbProviderFactory df = DbProviderFactories.GetFactory(DataProvider);
            // работа с подключением 
            using (DbConnection cn = df.CreateConnection())
            {
                cn.ConnectionString = ConnString;
                cn.Open();
                // объект команды
                DbCommand cmd = df.CreateCommand();
                cmd.Connection = cn;
                cmd.CommandText = CommandString;

                // используем объект чтения
                cmd.ExecuteNonQuery();
            }
            return;
        }

    }

    // ***************************** Статический класс со вспомогательными функциями*******************
    static class Utility
    {
        public static string MakeSQLDate(DateTime DT){
            return "'" + DT.Month.ToString() + "." + DT.Day.ToString() + "." + DT.Year.ToString() + "'";
        }
    }
}
