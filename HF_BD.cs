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
        public string OperationType;
        public string Source;
        public string Destination;
        public double Sum;
    }

    #endregion

    //********************* Абстрактный объект для всех компонентов систумы учета(справочников, операций, регистров...) 
    public abstract class AccountingSystemObject
    {
        protected DataBaseAccessProvider DBAccessProvider;
        protected string CommandString;
        protected void ReferToDataBase()
        {
            DBAccessProvider.CommandString = CommandString;
            try{
                DBAccessProvider.Execute();
            }
            catch (Exception e){
                throw new Exception("Ошибка доступа к БД.\n\tСообщение: " + e.Message + "\n\tСтек вызовов: " + e.StackTrace, e);
            }
            finally{                // Обязательная очистка командной строки
                CommandString = "";
            }
        }
        public AccountingSystemObject()
        {
            DBAccessProvider = new DataBaseAccessProvider();
            CommandString = "";
        }
    }

    //********************* Классы Cправочник  и Элемент справочника**********************************************
    public class Reference : AccountingSystemObject
    {
        string ReferenceName;
        List<ReferenceItem> ItemsList;

        public Reference(string ReferenceName) : base()
        {
            this.ReferenceName = ReferenceName;  // еще надо бы проверить корректность имени справочника
            ItemsList = new List<ReferenceItem>();
            MakeSQLCommandItemsList();
            ReferToDataBase();
            RefreshItemsList();
        }
        public void AddItem(string ItemName)
        {
            MakeSQLCommandAddItem(ItemName);
            MakeSQLCommandItemsList();
            ReferToDataBase();
            RefreshItemsList();
        }
        public List<string> GetNamesList()
        {
            List<string> NamesList = new List<string>();
            foreach (ReferenceItem item in ItemsList) {
                NamesList.Add(item.Name);
            }
            return NamesList;
        }
        public void DeleteItem(string ItemName)
        {
            MakeSQLCommandDeleteItem(ItemName);
            MakeSQLCommandItemsList();
            ReferToDataBase();
            RefreshItemsList();
        }
        public int GetID(string Name)
        {
            foreach (ReferenceItem item in ItemsList)
                if (item.Name == Name) return item.ID;
            throw new Exception("Reference.GetID: элемент '" + Name + "' справочника '"+ ReferenceName + "' не обнаружен!");
        }
        public string GetName(int ID)
        {
            foreach (ReferenceItem item in ItemsList)
                if (item.ID == ID) return item.Name;
            throw new Exception("Reference.GetID: элемент с ID = " + ID + " справочника '" + ReferenceName + "' не обнаружен!");
        }
        public bool isItemExists(string ItemName)
        {
            foreach (ReferenceItem item in ItemsList)
                if (item.Name == ItemName) return true;
            return false;
        }

        void MakeSQLCommandItemsList(){
            CommandString += "SELECT * FROM HomeFinance.dbo." + ReferenceName + "; ";
        }
        void MakeSQLCommandAddItem(string ItemName){
             CommandString += "INSERT INTO HomeFinance.dbo." + ReferenceName + " (Name) VALUES (N\'" + ItemName + "\'); ";
        }
        void MakeSQLCommandDeleteItem(string ItemName){
            CommandString += "DELETE FROM HomeFinance.dbo." + ReferenceName + " WHERE Name = N\'" + ItemName + "\'; ";
        }
        
        void RefreshItemsList()
        {
            DbDataReader dr = DBAccessProvider.DataReader;
            while (dr.Read()){
                ItemsList.Add(new ReferenceItem((int)dr["ID"], dr["Name"].ToString()));
            }
        }
    }

    public class ReferenceItem
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public ReferenceItem(int ID, string Name)
        {
            this.ID = ID;
            this.Name = Name;
        }
    }

    //********************* Классы регистра оборотов *****************************************
    public class RegisterTurnoverReports:AccountingSystemObject
    {
        string RegisterName;
        public RegisterTurnoverReports(string RegisterName) : base(){
            this.RegisterName = RegisterName;
        }
        public List<TItemState> ReportTurnover(DateTime BeginDate, DateTime EndDate)
        {                       
            MakeSQLCommand(BeginDate, EndDate);
            ReferToDataBase();
            return GetTurnoverList();
        }
        void MakeSQLCommand(DateTime BeginDate, DateTime EndDate)
        {
            string strBeginDate = Utility.MakeSQLDate(BeginDate);    // строковое представление даты для SQL-запроса
            string strEndDate = Utility.MakeSQLDate(EndDate);
            CommandString = "SELECT E.Name as Expense, SUM(R.MoveSum) as Rez FROM HomeFinance.dbo.RT_Moves_" + RegisterName;
            CommandString = CommandString + " R, HomeFinance.dbo." + RegisterName + " E, HomeFinance.dbo.Operations O ";
            CommandString = CommandString + "WHERE E.ID = R.Name AND O.ID = R.Operation AND O.Date >=" + strBeginDate + " AND O.Date <=" + strEndDate;
            CommandString = CommandString + "group by E.Name ";
        }        
        void MakeSQLCommandReferenceItems(){
            CommandString = "SELECT ID, Name FROM HomeFinance.dbo." + RegisterName;
        }
        List<TItemState> GetTurnoverList()
        {
            List<TItemState> TurnoversList = new List<TItemState>();  // результирующий список            
            DbDataReader dr = DBAccessProvider.DataReader;
            while (dr.Read())
            {
                TItemState NewItem = new TItemState();
                NewItem.ItemName = dr["Expense"].ToString();
                NewItem.Sum = (double)dr["Rez"];
                TurnoversList.Add(NewItem);
            }
            return TurnoversList;
        }
    }

    class RegisterTurnovers: AccountingSystemObject
    {
        private string RegisterName;
        private string fieldDimension;
        private string strOperationDate;
        private string strDimensionID;

        public int OperationNumber { get; set; }
        public string Dimension
        {
            get { return fieldDimension; }
            set
            {
                if (isDimensionExists(value)){
                    fieldDimension = value;
                    strDimensionID = GetDimensionID();
                }
                else
                    throw new Exception("RegisterTurnovers.setDimension: элемент '"
                        + value + "' справочника '" + RegisterName + "' не существует!");
            }
        }

        public RegisterTurnovers(string RegisterName) : base(){
            this.RegisterName = RegisterName;
        }
        public void EnterMoving(DateTime OperationDate, double Sum)
        {
            strOperationDate = Utility.MakeSQLDate(OperationDate);
            MakeSQLCommandMove(Sum);
            ReferToDataBase();
        }
        bool isDimensionExists(string DimensionName) {  // проверка элемента справочника на существование
            return new Reference(RegisterName).isItemExists(DimensionName);
        }
        string GetDimensionID()
        {
            string strID;
            // получить ID нашего разреза учета 
            Reference Ref = new Reference(RegisterName);
            strID = Ref.GetID(fieldDimension).ToString();
            return strID;
        }
        void MakeSQLCommandMove(double Sum)
        {
            CommandString = CommandString + "INSERT INTO HomeFinance.dbo.RT_Moves_" + RegisterName + " (Operation, Name, MoveSum)";
            CommandString = CommandString + " VALUES (" + OperationNumber.ToString() + ", " + strDimensionID + ", " + Utility.SumToStringForSQL(Sum) + "); ";
        }
    }

    //********************* Классы регистра остатков *****************************************
    public class RegisterRestReports : AccountingSystemObject
    {
        string RegisterName;
        public RegisterRestReports(string RegisterName) : base(){
            this.RegisterName = RegisterName;
        }
        public List<TItemState> ReportRests(DateTime RestsDate)
        {
            // получим все кошельки из справочника            
            MakeSQLCommandReferenceItems();
            ReferToDataBase();
            // а затем обратимся к данным регстра остатков
            MakeSQLCommand(RestsDate);
            ReferToDataBase();
            return GetRestsList();
        }
        private void MakeSQLCommand(DateTime RestsDate)
        {
            DbDataReader dr = DBAccessProvider.DataReader;
            string strRestsDate = Utility.MakeSQLDate(RestsDate);
            CommandString = "";

            // в объекте чтения - поля ID, Name справочника. Сколько элементов, столько и делаем запросов по регистру 
            while (dr.Read())
            {
                string strID = dr["ID"].ToString();
                CommandString = CommandString + "SELECT top(1) R.Date as Dt, W.Name as Nm, R.Rest as Sum FROM HomeFinance.dbo.RR_Rests_Wallets R, HomeFinance.dbo.Wallets W ";
                CommandString = CommandString + "WHERE R.Date<=" + strRestsDate + " AND R.Name = W.ID AND R.Name = " + strID;
                CommandString = CommandString + " Order by Dt desc; ";
            }
        }        
        void MakeSQLCommandReferenceItems(){
            CommandString = "SELECT ID, Name FROM HomeFinance.dbo." + RegisterName;
        }
        private List<TItemState> GetRestsList()
        {
            DbDataReader dr = DBAccessProvider.DataReader;
            List<TItemState> RestsList = new List<TItemState>();

            do
            {
                if (dr.Read())
                {
                    TItemState NewItem = new TItemState();
                    NewItem.ItemName = dr["Nm"].ToString();
                    NewItem.Sum = (double)dr["Sum"];
                    RestsList.Add(NewItem);
                }
            } while (dr.NextResult());
            return RestsList;
        }
    }

    public class RegisterRests : AccountingSystemObject
    {
        private string RegisterName;
        private string fieldDimension;
        private DateTime OperationDate;
        private string strOperationDate;
        private string strDimensionID;

        public int OperationNumber { get; set; }
        public string Dimension
        {
            get { return fieldDimension; }
            set
            {
                if (isDimensionExists(value)){
                    fieldDimension = value;
                    strDimensionID = GetDimensionID();
                }else
                    throw new Exception("RegisterRest.setDimension: элемент '"
                        + value + "' справочника '" + RegisterName + "' не существует!");
            }
        }

        public RegisterRests(string RegisterName) : base(){
            this.RegisterName = RegisterName;
        }
        public void EnterMoving(DateTime OperationDate, double Sum)
        {
            strOperationDate = Utility.MakeSQLDate(OperationDate);
            this.OperationDate = OperationDate;

            MakeSQLCommandMove(Sum);
            ReferToDataBase();
            // корректировка остатков в двва шага - получение остатков на дату,
            // затем пересчет строк в таблице
            MakeSQLCommandRestsForCurrentDate(Sum);
            ReferToDataBase();
            MakeSQLCommandRestsReCount(Sum);
            ReferToDataBase();                
        }
        private void MakeSQLCommandMove(double Sum)
        {
            CommandString = CommandString + "INSERT INTO HomeFinance.dbo.RR_Moves_" + RegisterName + " (Operation, Name, MoveSum)";
            CommandString = CommandString + " VALUES (" + OperationNumber.ToString() + ", " + strDimensionID + ", " + Utility.SumToStringForSQL(Sum) + "); ";
        }
        private void MakeSQLCommandRestsForCurrentDate(double Sum){
            CommandString = "SELECT Date, Rest FROM HomeFinance.dbo.RR_Rests_" + RegisterName
                + " WHERE Date<=" + strOperationDate + " and Name=" + strDimensionID + " Order by Date desc";
        }
        private void MakeSQLCommandRestsReCount(double Sum)
        {
            DbDataReader dr = DBAccessProvider.DataReader;
            
            // есть ли строки в выборке?
            if (dr.HasRows)   // есть
            {
                dr.Read();
                // "Верхняя" дата равна текущей?
                if ((DateTime)dr["Date"] == OperationDate)    // да
                {
                    CommandString = CommandString + "UPDATE HomeFinance.dbo.RR_Rests_" + RegisterName 
                        + " SET Rest = Rest + " + Utility.SumToStringForSQL(Sum) 
                        + " WHERE Date = " + strOperationDate + " and Name=" + strDimensionID + "; ";
                }
                else      // нет
                {
                    double AddSum;
                    AddSum = Sum + double.Parse(dr["Rest"].ToString());
                    CommandString = CommandString + "INSERT INTO HomeFinance.dbo.RR_Rests_" + RegisterName 
                        + " (Date, Name, Rest) " 
                        + "VALUES (" + strOperationDate + ", " + strDimensionID + ", " + Utility.SumToStringForSQL(AddSum) + "); ";
                }
            }
            else   //нет строк в выборке
            {
                CommandString = CommandString + "INSERT INTO HomeFinance.dbo.RR_Rests_" + RegisterName + " (Date, Name, Rest) "
                    + "VALUES (" + strOperationDate + ", " + strDimensionID + ", " + Utility.SumToStringForSQL(Sum) + "); ";
            }

            // есть ли строки с датой большей даты операции ?
            // увеличиваем остатки во всех строках с датой большей даты текущей операции (если такие строки есть) на сумму текущей операции
            CommandString = CommandString + "UPDATE HomeFinance.dbo.RR_Rests_" + RegisterName 
                + " SET Rest = Rest + " + Utility.SumToStringForSQL(Sum) 
                + " WHERE Date > " + strOperationDate + " and Name=" + strDimensionID + "; ";
        }
        private string GetDimensionID()
        {
            string strID;
            // получить ID нашего разреза учета 
            Reference Ref = new Reference(RegisterName);
            strID = Ref.GetID(fieldDimension).ToString();
            return strID;
        }
        bool isDimensionExists(string DimensionName)
        {  // проверка элемента справочника на существование
            return new Reference(RegisterName).isItemExists(DimensionName);
        }
    }

    //*********************Класс операции ***********************************************************
    public class Operation : AccountingSystemObject
    {
        private DateTime fieldOperationDate;
        private int OperationNumber;
        private string strOperationDate;

        public OperationTypes TypeOfTheOperation { get; set; }
        public DateTime OperationDate
        {
            get { return fieldOperationDate; }
            set
            {
                fieldOperationDate = value;
                strOperationDate = Utility.MakeSQLDate(value);
            }
        }
        public string Source { get; set; }
        public string Destination { get; set; }

        public Operation() : base(){
            OperationNumber = DBAccessProvider.GetCurrentOperationID();
        }

        public void Enter(double Sum)
        {
            OperationNumber++;
            MakeSQLCommandAddToOperationTable(Sum);
            ReferToDataBase();
            EnterRegisterMoves(Sum);
        }
        private void EnterRegisterMoves(double Sum)
        {
            switch (TypeOfTheOperation)
            {
                case OperationTypes.Rest:     // операция введения остатков
                    EnterRegisterRest("Wallets", Destination, Sum);
                    break;
                case OperationTypes.Expense:   //операция добавления расходов
                    EnterRegisterRest("Wallets", Source, -Sum);
                    EnterRegisterTurnovers("Expenses", Destination, Sum);
                    break;
                case OperationTypes.Income:
                    EnterRegisterRest("Wallets", Destination, Sum);
                    EnterRegisterTurnovers("Incomes", Source, Sum);
                    break;
                case OperationTypes.Transfer:
                    EnterRegisterRest("Wallets", Source, -Sum);
                    EnterRegisterRest("Wallets", Destination, Sum);
                    break;
            }
        }
        private void EnterRegisterTurnovers(string RegisterName, string Dimension, double Sum)
        {
            RegisterTurnovers RT = new RegisterTurnovers(RegisterName);
            RT.Dimension = Dimension;
            RT.OperationNumber = OperationNumber;

            // если произошел сбой при добавлении записи в таблицу, 
            // необходимо откатить все записи, свзанные с этой операцией
            TryAndRollback(Sum, RT);
        }
        private void EnterRegisterRest(string RegisterName, string Dimension, double Sum)
        {
            RegisterRests RR = new RegisterRests(RegisterName);
            RR.Dimension = Dimension;
            RR.OperationNumber = OperationNumber;

            // если произошел сбой при добавлении записи в таблицу, 
            // необходимо откатить все записи, свзанные с этой операцией
            TryAndRollback(Sum, RR);
        }
        private void TryAndRollback(double Sum, AccountingSystemObject Register)
        {
            try
            {
                if (Register is RegisterRests)
                    ((RegisterRests)Register).EnterMoving(fieldOperationDate, Sum);
                else
                    ((RegisterTurnovers)Register).EnterMoving(fieldOperationDate, Sum);
            }
            catch (Exception e)
            {
                this.Delete(OperationNumber);
                throw new Exception("Ошибка при записи движения регистра! Произошел откат операции!", e);
            }
        }
        private void MakeSQLCommandAddToOperationTable(double Sum)
        {
            string OpNum = OperationNumber.ToString();
            string OpType = "\'" + TypeOfTheOperation.ToString() + "\'";
            string Src = Utility.MakeUnicodeStringForSQL(Source);
            string Dest = Utility.MakeUnicodeStringForSQL(Destination);
            string strSum = Utility.SumToStringForSQL(Sum);
            CommandString = "INSERT INTO HomeFinance.dbo.Operations (ID, Date, OperationType, Source, Destination, Sum)"
                + " VALUES (" + OpNum + ", " + strOperationDate + ", " + OpType + ", " + Src + ", " + Dest + ", " + strSum + "); ";
        }

        public List<TOperation> GetList(DateTime StartDate, DateTime EndDate)
        {
            MakeSQLCommandGetOperationsList(StartDate, EndDate);
            ReferToDataBase();
            return FillTheList();
        }
        private List<TOperation> FillTheList()
        {
            List<TOperation> OperationsList = new List<TOperation>();
            DbDataReader dr = DBAccessProvider.DataReader;
            try
            {
                while (dr.Read())
                {
                    TOperation NewItem = new TOperation();
                    NewItem.ID = (int)dr["ID"];
                    NewItem.Date = DateTime.Parse(dr["Date"].ToString());
                    NewItem.OperationType = dr["OperationType"].ToString();
                    NewItem.Source = dr["Source"].ToString();
                    NewItem.Destination = dr["Destination"].ToString();
                    NewItem.Sum = (double)dr["Sum"];
                    OperationsList.Add(NewItem);
                }
            }
            catch (Exception e){
                throw new Exception("Operation.FillTheList: ошибка преобразовании значений. Сообщение: " + e.Message, e);
            }
            return OperationsList;
        }
        private void MakeSQLCommandGetOperationsList(DateTime StartDate, DateTime EndDate)
        {
            string strBeginDate = Utility.MakeSQLDate(StartDate);
            string strEndDate = Utility.MakeSQLDate(EndDate);
            
            CommandString = "SELECT * FROM HomeFinance.dbo.Operations ";
            CommandString = CommandString + "WHERE Date>=" + strBeginDate + " AND Date<= " + strEndDate;
        }

        public void Delete (int NumberOfOperation)
        {

        }

    }

    //********************** Старый класс базы данных ***********************************************
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
            string csGetDate = "SELECT Date, Rest FROM HomeFinance.dbo.RR_Rests_" + RegisterName 
                + " WHERE Date<=" + strDate + " and Name=" + ID + " Order by Date desc";
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
    public class DataBaseAccessProvider
    {

        // поля для инициализации базы данных DataProvider и ConnString
        string DataProvider;
        string ConnString;

        // фабрика поставщиков и подключение
        DbProviderFactory ProviderFactory;
        DbConnection Connection;

        int CurrentOperationID;

        public string CommandString { set; get; } // строка команды
        public DbDataReader DataReader { get; set; } // объект чтения данных
        
        // конструктор
        public DataBaseAccessProvider()
        {
            // получаем провайдер данных и строку подклчения из конфигурационного файла
            DataProvider = ConfigurationManager.AppSettings["provider"];
            ConnString = ConfigurationManager.AppSettings["cnStr"];

            // получаем фабрику поставщиков и объект подключения
            ProviderFactory = DbProviderFactories.GetFactory(DataProvider);
            Connection = ProviderFactory.CreateConnection();
            Connection.ConnectionString = ConnString;
        }

        public int GetCurrentOperationID()
        {
            // находим максимальный номер OperationID
            CommandString = "select max(ID) as maxOpID from HomeFinance.dbo.Operations";
            int MaxOperationID; // здесь будет результат

            try{
                Execute();
            }
            catch (Exception e){
                throw new Exception("DataBaseAccessProvider.GetCurrentOperationID: Ошибка получения данных о номере операции. БД не инициализирована! Сообщение: " + e.Message, e);
            }

            DataReader.Read();
            if (DataReader.IsDBNull(0))
                MaxOperationID = -1;
            else
                MaxOperationID = (int)DataReader["maxOpID"];
            return MaxOperationID;
        }
        public void IncreaseCurrentOperationID()
        {
            CurrentOperationID++;
        }

        public void Execute()
        {
            DbCommand cmd = ProviderFactory.CreateCommand();
            if (Connection.State == System.Data.ConnectionState.Closed)
                Connection.Open();

            if (DataReader != null)
                if (!DataReader.IsClosed)
                    DataReader.Close();
            
            cmd.Connection = Connection;
            cmd.CommandText = CommandString;

            try{
                DataReader = cmd.ExecuteReader();
            }
            catch (DbException e)
            {
                Connection.Close();
                throw new Exception("При вводе операции произошла ошибка доступа к базе данных! " + e.Message, e);
            }
        }  
    }

    // ***************************** Статический класс со вспомогательными функциями*******************
    static class Utility
    {
        public static string MakeSQLDate(DateTime DT){
            return "'" + DT.Month.ToString() + "." + DT.Day.ToString() + "." + DT.Year.ToString() + "'";
        }
        public static string SumToStringForSQL(double Sum){
            return Sum.ToString("#####.00", CultureInfo.CreateSpecificCulture("en-US"));
        }  
        public static string MakeUnicodeStringForSQL(string s){
            return "N\'" + s + "\'";
        }
    }
}
