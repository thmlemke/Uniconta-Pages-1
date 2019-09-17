using UnicontaClient.Models;
using Uniconta.ClientTools;
using Uniconta.ClientTools.DataModel;
using Uniconta.ClientTools.Page;
using Uniconta.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Uniconta.ClientTools.Controls;
using UnicontaClient.Utilities;
using Uniconta.ClientTools.Util;
using System.ComponentModel.DataAnnotations;

using UnicontaClient.Pages;
namespace UnicontaClient.Pages.CustomPage
{
    public class GLClosingSheetLineLocal : GLClosingSheetLineClient
    {
        [Display(Name = "Total", ResourceType = typeof(GLDailyJournalText))]
        [NoSQL]
        public double Total { get { return _Total / 100d; } }
        long _Total;
        public void SetTotal(long total)
        {
            if (total != _Total)
            {
                _Total = total;
                NotifyPropertyChanged("Total");
            }
        }

        [Display(Name = "VatAmount", ResourceType = typeof(GLDailyJournalText))]
        [NoSQL]
        public double VatAmount { get { return _VatAmount; } }

        double _VatAmount;
        public void SetVatAmount(double amount)
        {
            if (_VatAmount != amount)
            {
                _VatAmount = amount;
                NotifyPropertyChanged("VatAmount");
            }
        }

        [Display(Name = "VatAmountOffset", ResourceType = typeof(GLDailyJournalText))]
        [NoSQL]
        public double VatAmountOffset { get { return _VatAmountOffset; } }

        double _VatAmountOffset;
        public void SetVatAmountOffset(double amount)
        {
            if (_VatAmountOffset != amount)
            {
                _VatAmountOffset = amount;
                NotifyPropertyChanged("VatAmountOffset");
            }
        }
    }

    public class GLClosingSheetLineGrid : CorasauDataGridClient
    {
        public override Type TableType { get { return typeof(GLClosingSheetLineLocal); } }
        public override bool Readonly { get { return false; } }
        public override IComparer GridSorting { get { return new GLClosingSheetLineSort(); } }
        internal string MasterAccount;

		public override bool AddRowOnPageDown()
        {
            var selectedItem = (GLClosingSheetLineLocal)this.SelectedItem;
			return (selectedItem != null && selectedItem._AmountCent != 0);
        }

		protected override void DataLoaded(UnicontaBaseEntity[] Arr)
		{
			var MasterAccount = this.MasterAccount;
			foreach (var lin in (GLClosingSheetLineLocal[])Arr)
				lin.Swap = (lin._Account != MasterAccount) && (lin._ShowOnAccount != MasterAccount);
		}
	}

    public partial class ClosingSheetLines : GridBasePage
    {
        public override string NameOfControl { get { return TabControls.ClosingSheetLines; } }
        List<UnicontaBaseEntity> masterList;
        GLAccountClosingSheetClient masterAccount;
        SQLCache AccountListCache;
        double primo;
        DateTime postingDate;
        bool anyChange;

        protected override Filter[] DefaultFilters()
        {
            Filter dateFilter = new Filter();
            dateFilter.name = "Date";
            string dateValue;
            if (masterList != null && masterList.Count == 2)
            {
                var closingsheet = masterList[0] as GLClosingSheetClient;
                dateValue = String.Format("{0:d}..{1:d}", closingsheet.FromDate, closingsheet.ToDate);
            }
            else
                return null;
            dateFilter.value = dateValue;
            return new Filter[] { dateFilter };
        }
        public override void AssignMultipleGrid(List<Uniconta.ClientTools.Controls.CorasauDataGrid> gridCtrls)
        {
            gridCtrls.Add(dgClosingSheetLine);
            gridCtrls.Add(dgAccountsTransGrid);
        }
        SynchronizeEntity syncEntity;
        public ClosingSheetLines(UnicontaBaseEntity master1, SynchronizeEntity _syncEntity, SQLCache AccountListCache)
            : base(_syncEntity, false)
        {
            InitializeComponent();
            syncEntity = _syncEntity;
            masterList = new List<UnicontaBaseEntity>();
            masterList.Add(master1);
            masterList.Add(syncEntity.Row);

            var ClosingMaster = master1 as Uniconta.DataModel.GLClosingSheet;
            if (ClosingMaster != null)
                postingDate = ClosingMaster._ToDate;
            else
                postingDate = BasePage.GetSystemDefaultDate();

            this.AccountListCache = AccountListCache;
            SetRibbonControl(localMenu, dgClosingSheetLine);
            localMenu.FilterGrid = dgAccountsTransGrid;
            var defFilters = DefaultFilters();
            localMenu.SetFilterDefaultFields(defFilters);
            dgClosingSheetLine.api = api;
            dgClosingSheetLine.BusyIndicator = busyIndicator;
            dgClosingSheetLine.masterRecords = masterList;
            dgAccountsTransGrid.api = api;
            dgAccountsTransGrid.masterRecords = masterList;
            localMenu.OnItemClicked += localMenu_OnItemClicked;
            dgAccountsTransGrid.RowDoubleClick += DgAccountsTransGrid_RowDoubleClick;
            dgClosingSheetLine.View.DataControl.CurrentItemChanged += DataControl_CurrentItemChanged;
            var Comp = api.CompanyEntity;
            RoundTo100 = Comp.RoundTo100;
            if (RoundTo100)
                Debit.HasDecimals = Credit.HasDecimals = Amount.HasDecimals = false;
            this.BeforeClose += ClosingSheetLines_BeforeClose;
        }

        public override Task InitQuery()
        {
            var acc = syncEntity.Row as GLAccountClosingSheetClient;
            BindClosingSheet(acc);
            var t = BindTransactionGrid();
            StartLoadCache(t);
            RecalculateSum();
            return t;
        }

        private void DgAccountsTransGrid_RowDoubleClick()
        {
            var trans = dgAccountsTransGrid.SelectedItem as GLTransClient;
            if(trans != null)
            {
                string vheader = string.Format("{0} ({1})", Uniconta.ClientTools.Localization.lookup("VoucherTransactions"), trans._Voucher);
                AddDockItem(TabControls.AccountsTransaction, dgAccountsTransGrid.syncEntity, vheader);
            }
        }

        void BindClosingSheet(GLAccountClosingSheetClient acc)
        {
            if (acc != null)
            {
                this.primo = acc.OrgBalance;
                this.masterAccount = acc;
                var Account = acc._Account;
                dgClosingSheetLine.MasterAccount = Account;

                List<GLClosingSheetLineLocal> lst = new List<GLClosingSheetLineLocal>();
                if (acc.Lines != null)
                {
                    foreach (var l in acc.Lines)
                    {
                        var lin = new GLClosingSheetLineLocal();
                        StreamingManager.Copy(l, lin);
                        lst.Add(lin);
                        lin.Swap = (lin._OffsetAccount == Account);
                    }
                }
                dgClosingSheetLine.ItemsSource = lst;
                dgClosingSheetLine.SelectRange(0, 0);
                dgClosingSheetLine.Visibility = Visibility.Visible;
            }
        }

        protected override void SyncEntityMasterRowChanged(UnicontaBaseEntity args)
        {
            var masters = masterList;
            masters.RemoveAt(1);
            masters.Add(args);
            masterList = masters;
            dgClosingSheetLine.masterRecords = masters;
            dgAccountsTransGrid.masterRecords = masters;
            SetHeader(args);
        }
        async void SetHeader(UnicontaBaseEntity args)
        {
            var masterClient = args as GLAccountClosingSheetClient;
            string header = string.Format("{0}: {1}, {2}", Uniconta.ClientTools.Localization.lookup("ClosingSheetLines"), masterClient._Account, ((GLClosingSheetClient)masterList[0])._Name);
            SetHeader(header);
            BindClosingSheet(masterClient);
            await BindTransactionGrid();
            RecalculateSum();
        }

        static public double CalcVat(string VatId, double Amount, DateTime Date, bool RoundTo100, SQLCache VatCache)
        {
            if (VatId == null || VatCache == null || Amount == 0d)
                return 0d;

            var vatpost = (Uniconta.DataModel.GLVat)VatCache.Get(VatId);
            if (vatpost == null)
                return 0d;

            double ret;
            if ((vatpost._Account != null || vatpost._FollowAccount) && vatpost._OffsetAccount == null)
                ret = vatpost.VatAmount1(Amount, Date, RoundTo100, null);
            else
                ret = 0d;
            if ((vatpost._AccountRate2 != null || vatpost._FollowAccount2) && vatpost._OffsetAccountRate2 == null)
                ret += vatpost.VatAmount2(Amount, Date, RoundTo100, null);

            return ret;
        }

        internal void RecalculateSum()
        {
            var gridItems = (IEnumerable<GLClosingSheetLineLocal>)dgClosingSheetLine.ItemsSource;
            if (gridItems == null)
                return;
            var sum = RecalculateSum(gridItems, dgClosingSheetLine.MasterAccount, this.postingDate, this.RoundTo100, VatCache);
            SetStatusText(this.primo, sum / 100d);
            masterAccount.setChangeAndNewBalance(sum);
        }

        static internal long RecalculateSum(IEnumerable<GLClosingSheetLineLocal> lines, string Account, DateTime postingDate, bool RoundTo100, SQLCache VatCache)
        {
            long sum = 0;
            foreach (var lin in lines)
            {
                if (lin._Account == Account)
                {
                    sum += lin._AmountCent;
                    var vatValue = CalcVat(lin._Vat, lin._Amount, postingDate, RoundTo100, VatCache);
                    lin.SetVatAmount(vatValue);
                    if (vatValue != 0d)
                        sum -= Uniconta.Common.Utility.NumberConvert.ToLong(vatValue * 100d);
                }
                else if (lin._OffsetAccount == Account)
                {
                    sum -= lin._AmountCent;
                    var vatValue = CalcVat(lin._OffsetVat, lin._Amount, postingDate, RoundTo100, VatCache);
                    lin.SetVatAmountOffset(-vatValue);
                    if (vatValue != 0d)
                        sum += Uniconta.Common.Utility.NumberConvert.ToLong(vatValue * 100d);
                }
                lin.SetTotal(sum);
            }
            return sum;
        }

        bool RoundTo100;
        internal double LineTotal;
        void SetStatusText(double primo, double sum)
        {
            string format = RoundTo100 ? "N0" : "N2";
            RibbonBase rb = (RibbonBase)localMenu.DataContext;
            var groups = UtilDisplay.GetMenuCommandsByStatus(rb, true);

            var thisyear = Uniconta.ClientTools.Localization.lookup("ThisYear");
            var Change = Uniconta.ClientTools.Localization.lookup("Change");
            var NewBalance = Uniconta.ClientTools.Localization.lookup("NewBalance");
            foreach (var grp in groups)
            {
                if (grp.Caption == thisyear)
                    grp.StatusValue = primo.ToString(format);
                else if (grp.Caption == Change)
                    grp.StatusValue = sum.ToString(format);
                else if (grp.Caption == NewBalance)
                {
                    LineTotal = primo + sum;
                    grp.StatusValue = LineTotal.ToString(format);
                }
                else grp.StatusValue = string.Empty;
            }
        }

        SQLCache TextTypes, VatCache;
        protected override async void LoadCacheInBackGround()
        {
            var api = this.api;
            var Comp = api.CompanyEntity;

            VatCache = Comp.GetCache(typeof(Uniconta.DataModel.GLVat)) ?? await Comp.LoadCache(typeof(Uniconta.DataModel.GLVat), api).ConfigureAwait(false);
            TextTypes = Comp.GetCache(typeof(Uniconta.DataModel.GLTransType)) ?? await Comp.LoadCache(typeof(Uniconta.DataModel.GLTransType), api).ConfigureAwait(false);
        }

        void DataControl_CurrentItemChanged(object sender, DevExpress.Xpf.Grid.CurrentItemChangedEventArgs e)
        {
            var oldselectedItem = e.OldItem as GLClosingSheetLineLocal;
            if (oldselectedItem != null)
                oldselectedItem.PropertyChanged -= JournalLineGridClient_PropertyChanged;

            var selectedItem = e.NewItem as GLClosingSheetLineLocal;
            if (selectedItem != null)
                selectedItem.PropertyChanged += JournalLineGridClient_PropertyChanged;
        }

        void JournalLineGridClient_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Uniconta.DataModel.GLTransType t;
            var rec = sender as GLClosingSheetLineLocal;
            switch (e.PropertyName)
            {
                case "Text":
                    t = UnicontaClient.Pages.GLDailyJournalLine.getTransText(rec._Text, TextTypes);
                    if (t != null)
                    {
                        rec.Text = t._TransType;
                        if (t._Account != null && t._AccountType == 0)
                            rec.Account = t._Account;
                        if (t._OffsetAccount != null && t._OffsetAccountType == 0)
                            rec.OffsetAccount = t._OffsetAccount;
                    }
                    break;
                case "OffsetText":
                    t = UnicontaClient.Pages.GLDailyJournalLine.getTransText(rec._OffsetText, TextTypes);
                    if (t != null)
                    {
                        rec.OffsetText = t._TransType;
                        if (t._Account != null && t._AccountType == 0)
                            rec.OffsetAccount = t._Account;
                        if (t._OffsetAccount != null && t._OffsetAccountType == 0)
                            rec.Account = t._OffsetAccount;
                    }
                    break;
                case "Amount":
                case "Debit":
                case "Credit":
                case "Account":
                case "OffsetAccount":
                case "Vat":
                case "OffsetVat":
                    RecalculateSum();
                    break;
            }
        }

        private void ClosingSheetLines_BeforeClose()
        {
            if (anyChange)
                globalEvents.OnRefresh(TabControls.ClosingSheetLines);
        }

        Task BindTransactionGrid()
        {
            return FilterGrid(dgAccountsTransGrid, true, false);
        }

        private void localMenu_OnItemClicked(string ActionType)
        {
            var trans = dgAccountsTransGrid.SelectedItem as GLTransClient;

            switch (ActionType)
            {
                case "AddRow":
                    dgClosingSheetLine.AddRow();
                    break;
                case "CopyRow":
                    dgClosingSheetLine.CopyRow();
                    RecalculateSum();
                    break;
                case "SaveGrid":
                    saveGrid();
                    break;
                case "DeleteRow":
                    dgClosingSheetLine.DeleteRow();
                    RecalculateSum();
                    break;
                case "VoucherTransactions":
                    if (trans == null)
                        return;
                    string vheader = string.Format("{0} ({1})", Uniconta.ClientTools.Localization.lookup("VoucherTransactions"), trans._Voucher);
                    AddDockItem(TabControls.AccountsTransaction, dgAccountsTransGrid.syncEntity, vheader);
                    break;
                case "ReverseVoucher":
                    if (trans == null)
                        return;
                    GLClosingSheetLineLocal closingLine = new GLClosingSheetLineLocal();
                    closingLine.Account = trans._Account;
                    closingLine.Text = trans._Text;
                    closingLine.TransType = trans._TransType;
                    closingLine.Dimension1 = trans._Dimension1;
                    closingLine.Dimension2 = trans._Dimension2;
                    closingLine.Dimension3 = trans._Dimension3;
                    closingLine.Dimension4 = trans._Dimension4;
                    closingLine.Dimension5 = trans._Dimension5;
                    closingLine.Amount = -trans._Amount;
                    dgClosingSheetLine.AddRow(closingLine);
                    RecalculateSum();
                    break;
                default:
                    gridRibbon_BaseActions(ActionType);
                    break;
            }
        }

        void SumLines(GLAccountClosingSheetClient acc)
        {
            var sum = RecalculateSum((IEnumerable<GLClosingSheetLineLocal>)acc.Lines, acc._Account, this.postingDate, this.RoundTo100, this.VatCache);
            acc.setChangeAndNewBalance(sum);
        }

        GLAccountClosingSheetClient FindAccount(string Account)
        {
            return (GLAccountClosingSheetClient)this.AccountListCache.Get(Account);
        }

        static GLClosingSheetLineLocal FindLine(IEnumerable<GLClosingSheetLineClient> Lines, int RowId)
        {
            foreach (var rec in Lines)
                if (rec.RowId == RowId)
                    return (GLClosingSheetLineLocal)rec;
            return null;
        }

        void removeAccount(string account, int RowId)
        {
            if (account != null && account != masterAccount._Account) // we can't remove from the list we loop over
            {
                var acc = FindAccount(account);
                if (acc?.Lines != null)
                {
                    var linOffset = FindLine(acc.Lines, RowId);
                    if (linOffset != null) // we found line
                    {
                        ((List<GLClosingSheetLineLocal>)acc.Lines).Remove(linOffset);
                        SumLines(acc);
                    }
                }
            }
        }
        void insertOrUpdateAccount(string account, GLClosingSheetLineLocal lin, GLClosingSheetLineLocal clone)
        {
            if (account != null && account != masterAccount._Account)
            {
                var acc = FindAccount(account);
                if (acc != null)
                {
                    var accLines = acc.Lines;
                    if (accLines == null)
                        acc.Lines = new List<GLClosingSheetLineLocal>() { clone };
                    else
                    {
                        var linOffset = FindLine(accLines, lin.RowId);
                        if (linOffset == null)
                            ((List<GLClosingSheetLineLocal>)accLines).Add(clone);
                        else
                            StreamingManager.Copy(lin, linOffset);
                    }
                    SumLines(acc);
                }
            }
        }

        protected async override Task<ErrorCodes> saveGrid()
        {
            if (! dgClosingSheetLine.HasUnsavedData)
                return ErrorCodes.Succes;

            anyChange = true;
            dgClosingSheetLine.SelectedItem = null;
            var err = await dgClosingSheetLine.SaveData();

            if (err == 0 && masterAccount != null)
            {
                var Account = masterAccount._Account;
                var Lines = dgClosingSheetLine.ItemsSource as List<GLClosingSheetLineLocal>;

                if (masterAccount.Lines != null)
                {
                    // first loop over the old lines and find lines that has been deleted or offset account has changed
                    foreach (var oldLin in masterAccount.Lines)
                    {
                        var RowId = oldLin.RowId;
                        var newLin = FindLine(Lines, RowId);
                        if (newLin != null)
                        {
                            if (newLin._Account != oldLin._Account)
                                removeAccount(oldLin._Account, RowId);
                            if (newLin._OffsetAccount != oldLin._OffsetAccount)
                                removeAccount(oldLin._OffsetAccount, RowId);
                        }
                        else  // line has been delete
                        {
                            removeAccount(oldLin._Account, RowId);
                            removeAccount(oldLin._OffsetAccount, RowId);
                            if (oldLin._Account != oldLin._ShowOnAccount)
                                removeAccount(oldLin._ShowOnAccount, RowId);
                        }
                    }
                }

                // loop over the new lines and update offset accounts
                List<GLClosingSheetLineLocal> lst = new List<GLClosingSheetLineLocal>();
                foreach (var lin in Lines)
                {
                    var clone = new GLClosingSheetLineLocal();
                    StreamingManager.Copy(lin, clone);
                    insertOrUpdateAccount(lin._Account, lin, clone);
                    insertOrUpdateAccount(lin._OffsetAccount, lin, clone);
                    if (lin._Account != lin._ShowOnAccount)
                        insertOrUpdateAccount(lin._ShowOnAccount, lin, clone);
                    lst.Add(clone);
                }

                masterAccount.Lines = lst;
                SumLines(masterAccount);
            }

            BindTransactionGrid();
            return err;
        }

        protected override void OnLayoutLoaded()
        {
            base.OnLayoutLoaded();
            var api = this.api;
            UnicontaClient.Utilities.Utility.SetDimensionsGrid(api, cldim1, cldim2, cldim3, cldim4, cldim5);
            UnicontaClient.Utilities.Utility.SetDimensionsGrid(api, coldim1, coldim2, coldim3, coldim4, coldim5);
            UnicontaClient.Utilities.Utility.SetDimensionsGrid(api, colOffsetdim1, colOffsetdim2, colOffsetdim3, colOffsetdim4, colOffsetdim5);

            var offsetName = Uniconta.ClientTools.Localization.lookup("OffsetAccount");
            var c = api.CompanyEntity;
            colOffsetdim1.Header = string.Format("{1} ({0})", offsetName, c._Dim1);
            colOffsetdim2.Header = string.Format("{1} ({0})", offsetName, c._Dim2);
            colOffsetdim3.Header = string.Format("{1} ({0})", offsetName, c._Dim3);
            colOffsetdim4.Header = string.Format("{1} ({0})", offsetName, c._Dim4);
            colOffsetdim5.Header = string.Format("{1} ({0})", offsetName, c._Dim5);
        }
    }
}