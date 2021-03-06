using Uniconta.API.DebtorCreditor;
using UnicontaClient.Models;
using Uniconta.ClientTools;
using Uniconta.ClientTools.Controls;
using Uniconta.ClientTools.DataModel;
using Uniconta.ClientTools.Page;
using Uniconta.Common;
using Uniconta.DataModel;
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
using System.Windows.Threading;
using Uniconta.ClientTools.Util;

using Uniconta.API.GeneralLedger;
using System.Text;
using UnicontaClient.Utilities;
using System.ComponentModel.DataAnnotations;
using UnicontaClient.Controls.Dialogs;
using DevExpress.Xpf.Grid;
using System.ComponentModel;
using UnicontaClient.Pages;

using UnicontaClient.Pages;
namespace UnicontaClient.Pages.CustomPage
{
    public class BankStatementLineGrid : CorasauDataGridClient
    {
#if !SILVERLIGHT
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            tableView.RowStyle = Application.Current.Resources["MatchingRowStyle"] as Style;
        }
#else
        protected override DataTemplate RowTemplate()
        {
            return Application.Current.Resources["customDataRowTemplate"] as DataTemplate;
        }
#endif
        public override Type TableType { get { return typeof(BankStatementLineGridClient); } }
        public override IComparer GridSorting { get { return new BankStatementLineSort(); } }
        public override bool Readonly { get { return false; } }
        public override bool CanInsert { get { return false; } }
        public override bool IsAutoSave { get { return true; } }
    }

    public class TransGrid : CorasauDataGridClient
    {
#if !SILVERLIGHT
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            tableView.RowStyle = Application.Current.Resources["MatchingRowStyle"] as Style;
        }
#else
        protected override DataTemplate RowTemplate()
        {
            return Application.Current.Resources["customDataRowTemplate"] as DataTemplate;
        }
#endif
        public override Type TableType { get { return typeof(GLTransClientTotalBank); } }
    }

    public partial class BankStatementLinePage : GridBasePage
    {
        public override string NameOfControl { get { return TabControls.BankStatementLinePage; } }
        static DateTime fromDate;
        static DateTime toDate;

        Uniconta.API.GeneralLedger.ReportAPI tranApi;
        BankStatementAPI bankTransApi;
        readonly bool ShowCurrency;
        Uniconta.DataModel.BankStatement master;
        int DaysSlip;
        string showAmountType;
        GLTransClientTotalBank[] GlTransList;
        BankStatementLineGridClient[] BankList;
        Orientation orient;

        string bankStatCaption;
        string transactionCaption;

        public BankStatementLinePage(UnicontaBaseEntity sourceData)
            : base(sourceData)
        {
            InitializeComponent();
            bankStatCaption = Uniconta.ClientTools.Localization.lookup("BankStatement");
            transactionCaption = Uniconta.ClientTools.Localization.lookup("Transactions");
            master = sourceData as Uniconta.DataModel.BankStatement;
            DaysSlip = master._DaysSlip;

            if (fromDate == DateTime.MinValue)
            {
                DateTime date = DateTime.Today;
                var firstDayOfMonth = new DateTime(date.Year, date.Month, 1);
                toDate = firstDayOfMonth.AddMonths(1).AddDays(-1);
                fromDate = firstDayOfMonth.AddMonths(-2);
            }

            bool RoundTo100;
            var Comp = api.CompanyEntity;
            if (Comp.SameCurrency(master._Currency))
                RoundTo100 = Comp.RoundTo100;
            else
            {
                ShowCurrency = true;
                RoundTo100 = !CurrencyUtil.HasDecimals(master._Currency);
            }
            if (RoundTo100)
            {
                Debit.HasDecimals = Credit.HasDecimals = Amount.HasDecimals = Total.HasDecimals = false;
                Debitcol.HasDecimals = Creditcol.HasDecimals = Amountcol.HasDecimals = Totalcol.HasDecimals = false;
            }

            if (!Comp._UseVatOperation)
                VatOperation.Visible = false;

            dgBankStatementLine.api = api;
            tranApi = new Uniconta.API.GeneralLedger.ReportAPI(api);
            bankTransApi = new BankStatementAPI(api);
            SetRibbonControl(localMenu, dgBankStatementLine);
            dgBankStatementLine.BusyIndicator = busyIndicator;
            dgAccountsTransGrid.BusyIndicator = busyIndicator;
            dgAccountsTransGrid.api = api;
            localMenu.OnItemClicked += localMenu_OnItemClicked;
            dgBankStatementLine.View.DataControl.CurrentItemChanged += DataControl_CurrentItemChanged;
            dgAccountsTransGrid.View.DataControl.CurrentItemChanged += DataControl_CurrentItemChanged1;
            State.Header = Uniconta.ClientTools.Localization.lookup("Status");
            StateCol.Header = Uniconta.ClientTools.Localization.lookup("Status");
            SetStatusText();
            Mark.Visible = false;
            MarkCol.Visible = false;
            GetShowHideGreenMenuItem();

            orient = api.session.Preference.BankStatementHorisontal ? Orientation.Horizontal : Orientation.Vertical;
            lGroup.Orientation = orient;

            this.showAmountType = Uniconta.ClientTools.Localization.lookup("All");
            dgBankStatementLine.View.SearchControl = localMenu.SearchControl;
            ribbonControl.lowerSearchGrid = dgAccountsTransGrid;
            ribbonControl.UpperSearchNullText = bankStatCaption;
            ribbonControl.LowerSearchNullText = transactionCaption;
          
#if SILVERLIGHT
            Application.Current.RootVisual.KeyDown += RootVisual_KeyDown;
#else
            this.PreviewKeyDown += RootVisual_KeyDown;
#endif
            this.BeforeClose += BankStatementLinePage_BeforeClose;
        }

        public override void PageClosing()
        {
            if (dgBankStatementLine.IsAutoSave && IsDataChaged)
                saveGrid();
            base.PageClosing();
        }

        private void BankStatementLinePage_BeforeClose()
        {
#if SILVERLIGHT
            Application.Current.RootVisual.KeyDown -= RootVisual_KeyDown;
#else
            this.PreviewKeyDown -= RootVisual_KeyDown;
#endif
        }

        private void RootVisual_KeyDown(object sender, KeyEventArgs e)
        {
#if !SILVERLIGHT
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                if (dgBankStatementLine.CurrentColumn.Name == "HasOffsetAccounts" && e.Key == Key.Down)
                {
                    var currentRow = dgBankStatementLine.SelectedItem as BankStatementLineGridClient;
                    if (currentRow != null)
                        CallOffsetAccount(currentRow);
                }
            }
#endif
        }

        private void DataControl_CurrentItemChanged1(object sender, CurrentItemChangedEventArgs e)
        {
            GLTransClientTotalBank oldselectedItem = e.OldItem as GLTransClientTotalBank;
            if (oldselectedItem != null)
                oldselectedItem.PropertyChanged -= selectedItem_PropertyChanged1;

            GLTransClientTotalBank selectedItem = e.NewItem as GLTransClientTotalBank;
            if (selectedItem != null)
                selectedItem.PropertyChanged += selectedItem_PropertyChanged1;
        }

        protected override void OnLayoutLoaded()
        {
            base.OnLayoutLoaded();
            setDim();
            Amountcol.Visible = !this.ShowCurrency;
            AmountCur.Visible = this.ShowCurrency;
        }

        protected override LookUpTable HandleLookupOnLocalPage(LookUpTable lookup, CorasauDataGrid dg)
        {
            return AccountsTransaction.HandleLookupOnLocalPage(dg, lookup);
        }

        void DataControl_CurrentItemChanged(object sender, DevExpress.Xpf.Grid.CurrentItemChangedEventArgs e)
        {
            BankStatementLineGridClient oldselectedItem = e.OldItem as BankStatementLineGridClient;
            if (oldselectedItem != null)
                oldselectedItem.PropertyChanged -= selectedItem_PropertyChanged;

            BankStatementLineGridClient selectedItem = e.NewItem as BankStatementLineGridClient;
            if (selectedItem != null)
            {
                selectedItem.PropertyChanged += selectedItem_PropertyChanged;
                // SetAccountSource(selectedItem);
            }
        }

        SQLCache LedgerCache, DebtorCache, CreditorCache;
        protected override async void LoadCacheInBackGround()
        {
            var api = this.api;
            LedgerCache = api.GetCache(typeof(Uniconta.DataModel.GLAccount)) ?? await api.LoadCache(typeof(Uniconta.DataModel.GLAccount)).ConfigureAwait(false);
            DebtorCache = api.GetCache(typeof(Uniconta.DataModel.Debtor)) ?? await api.LoadCache(typeof(Uniconta.DataModel.Debtor)).ConfigureAwait(false);
            CreditorCache = api.GetCache(typeof(Uniconta.DataModel.Creditor)) ?? await api.LoadCache(typeof(Uniconta.DataModel.Creditor)).ConfigureAwait(false);
        }

        CorasauGridLookupEditorClient prevAccount;
        private void Account_GotFocus(object sender, RoutedEventArgs e)
        {
            BankStatementLineGridClient selectedItem = dgBankStatementLine.SelectedItem as BankStatementLineGridClient;
            if (selectedItem != null)
            {
                SetAccountSource(selectedItem);
                if (prevAccount != null)
                    prevAccount.isValidate = false;
                var editor = (CorasauGridLookupEditorClient)sender;
                prevAccount = editor;
                editor.isValidate = true;
            }
        }
        private void SetAccountSource(BankStatementLineGridClient rec)
        {
            SQLCache cache;
            switch (rec._AccountType)
            {
                case (byte)GLJournalAccountType.Finans:
                    cache = LedgerCache;
                    break;
                case (byte)GLJournalAccountType.Debtor:
                    cache = DebtorCache;
                    break;
                case (byte)GLJournalAccountType.Creditor:
                    cache = CreditorCache;
                    break;
                default: return;
            }

            if (cache != null)
            {
                int ver = cache.version + 10000 * (rec._AccountType + 1);
                if (ver != rec.AccountVersion)
                {
                    rec.AccountVersion = ver;
                    rec.accntSource = cache.GetNotNullArray;
                    rec.NotifyPropertyChanged("AccountSource");
                }
            }
        }

        DCAccount getDCAccount(GLJournalAccountType type, string Acc)
        {
            if (type == GLJournalAccountType.Finans || Acc == null)
                return null;
            var cache = (type == GLJournalAccountType.Debtor) ? DebtorCache : CreditorCache;
            return (DCAccount)cache.Get(Acc);
        }
        DCAccount copyDCAccount(BankStatementLineGridClient rec, GLJournalAccountType type, string Acc)
        {
            var dc = getDCAccount(type, Acc);
            if (dc == null)
                return null;
            if (dc._Dim1 != null)
                rec.Dimension1 = dc._Dim1;
            if (dc._Dim1 != null)
                rec.Dimension2 = dc._Dim2;
            if (dc._Dim1 != null)
                rec.Dimension3 = dc._Dim3;
            if (dc._Dim1 != null)
                rec.Dimension4 = dc._Dim4;
            if (dc._Dim1 != null)
                rec.Dimension5 = dc._Dim5;
            return dc;
        }
        async void GetSearchOpenInvoice(BankStatementLineGridClient line, GLJournalAccountType type)
        {
            InvoiceAPI InvApi = new InvoiceAPI(api);
            string strAccount = (string)await InvApi.SearchOpenInvoice(type, line._Invoice);
            if (strAccount != null)
                line.Account = strAccount;
        }

        void selectedItem_PropertyChanged1(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Mark":
                    long val;
                    var lstAct = ((IEnumerable<GLTransClientTotalBank>)dgAccountsTransGrid.ItemsSource);
                    if (ShowCurrency)
                        val = (from t in lstAct where t._Mark select t._AmountCurCent).Sum();
                    else
                        val = (from t in lstAct where t._Mark select t._AmountCent).Sum();
                    this.layOutTrans.Caption = string.Format("{0}  '{1:N2}'", transactionCaption, (val / 100d));
                    break;
            }
        }

        void selectedItem_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var rec = sender as BankStatementLineGridClient;
            switch (e.PropertyName)
            {
                case "AccountType":
                    SetAccountSource(rec);
                    break;
                case "Invoice":
                    if (rec._Invoice != 0)
                    {
                        GLJournalAccountType type = rec._AmountCent > 0 ? GLJournalAccountType.Debtor : GLJournalAccountType.Creditor;
                        rec.AccountType = AppEnums.GLAccountType.ToString((int)type);
                        GetSearchOpenInvoice(rec, type);
                    }
                    break;
                case "Account":
                    if (rec._AccountType != (byte)GLJournalAccountType.Finans)
                    {
                        var dc = copyDCAccount(rec, (GLJournalAccountType)rec._AccountType, rec._Account);
                        if (dc != null)
                        {
                            rec.Vat = null;
                            rec.VatOperation = null;
                        }
                    }
                    else
                    {
                        var Acc = (GLAccount)LedgerCache.Get(rec._Account);
                        if (Acc != null)
                        {
                            if (Acc._IsDCAccount || Acc._MandatoryTax == VatOptions.NoVat)
                            {
                                rec.Vat = null;
                                rec.VatOperation = null;
                            }
                            else
                            {
                                rec.Vat = Acc._Vat;
                                rec.VatOperation = Acc._VatOperation;
                            }
                        }
                    }
                    break;
                case "Mark":
                    var lstBst = ((IEnumerable<BankStatementLineGridClient>)dgBankStatementLine.ItemsSource);
                    var val = (from t in lstBst where t._Mark select t._AmountCent).Sum();
                    this.layOutBankStat.Caption = string.Format("{0}  '{1:N2}'", bankStatCaption, (val / 100d));
                    break;
            }
        }

        public override void AssignMultipleGrid(List<Uniconta.ClientTools.Controls.CorasauDataGrid> gridCtrls)
        {
            gridCtrls.Add(dgBankStatementLine);
            gridCtrls.Add(dgAccountsTransGrid);
            isChildGridExist = true;
        }

        public async override Task InitQuery()
        {
            busyIndicator.IsBusy = true;

            // load gltrans without awit
            var glTransTask = tranApi.GetBank(new GLTransClientTotalBank(), master._Account, fromDate, toDate);

            var bankStmtLines = (BankStatementLineGridClient[])await bankTransApi.GetTransactions(new BankStatementLineGridClient(), master, fromDate, toDate, true);

            if (showAmountType == Uniconta.ClientTools.Localization.lookup("Debit"))
            {
                var stmtLines = bankStmtLines.Where(x => x._AmountCent >= 0).ToArray();
                bankStmtLines = stmtLines;

            }
            else if (showAmountType == Uniconta.ClientTools.Localization.lookup("Credit"))
            {
                var stmtLines = bankStmtLines.Where(x => x._AmountCent <= 0).ToArray();
                bankStmtLines = stmtLines;
            }

            this.layOutBankStat.Caption = bankStatCaption;
            this.layOutTrans.Caption = transactionCaption;

            var listtran = (GLTransClientTotalBank[])await glTransTask;  // wait for gltrans
            int llisttranLen = 0;

            busyIndicator.IsBusy = false;

            var ShowCurrency = this.ShowCurrency;
            if (listtran != null)
            {
                long Total = 0;
                llisttranLen = listtran.Length;
                for (int i = 0; (i < llisttranLen); i++)
                {
                    var p = listtran[i];
                    Total += ShowCurrency ? p._AmountCurCent : p._AmountCent;
                    p._Total = Total;
                }
            }
            if (bankStmtLines != null)
            {
                DateTime LastPostedDate = DateTime.MinValue;
                int startGLSearch = 0;
                long Total = 0;
                int l = bankStmtLines.Length;
                if (l > 0)
                    bankStmtLines[0]._AmountCent += Uniconta.Common.Utility.NumberConvert.ToLong(100d * master._StartBalance);

                for (int i = 0; (i < l); i++)
                {
                    var p = bankStmtLines[i];
                    if (p._Primo)
                        p._Text = Uniconta.ClientTools.Localization.lookup("Primo");
                    //if (! p._Void)
                    {
                        Total += p._AmountCent;
                        p._Total = Total;
                    }

                    var PostedDate = p._PostedDate;
                    if (PostedDate != DateTime.MinValue)
                    {
                        if (PostedDate >= LastPostedDate)
                            LastPostedDate = PostedDate;
                        else
                            startGLSearch = 0;

                        for (int n = startGLSearch; (n < llisttranLen); n++)
                        {
                            var t = listtran[n];
                            if (t._Date < PostedDate)
                                startGLSearch = n;
                            else if (t._Date == PostedDate)
                            {
                                if (t._Voucher == p._Voucher && t._VoucherLine == p._VoucherLine && t._JournalPostedId == p._JournalPostedId)
                                {
                                    if (p._Trans == null)
                                        p._Trans = new List<GLTransClientTotalBank>();
                                    p._Trans.Add(t);

                                    if (t._StatementLines == null)
                                        t._StatementLines = new List<BankStatementLineGridClient>();
                                    t._StatementLines.Add(p);

                                    if (p.MultiMark != null)
                                    {
                                        foreach (var mark in p.MultiMark)
                                        {
                                            PostedDate = mark.Date;
                                            int cnt = startGLSearch;
                                            while (++cnt < llisttranLen)
                                            {
                                                t = listtran[cnt];
                                                if (t._Date == PostedDate)
                                                {
                                                    if (t._Voucher == mark.Voucher && t._VoucherLine == mark.VoucherLine && t._JournalPostedId == mark.JournalPostedId
                                                        && (ShowCurrency ? t._AmountCur : t._Amount) == mark.Amount)
                                                    {
                                                        p._Trans.Add(t);
                                                        if (t._StatementLines == null)
                                                            t._StatementLines = new List<BankStatementLineGridClient>();
                                                        t._StatementLines.Add(p);
                                                        t._Reconciled = true;
                                                        break;
                                                    }
                                                }
                                                else if (t._Date > PostedDate)
                                                    break;
                                            }
                                        }
                                    }
                                    break;
                                }
                            }
                            else
                                break;
                        }
                    }
                }
            }
            GlTransList = listtran;
            dgAccountsTransGrid.SetSource(listtran);
            BankList = bankStmtLines;
            dgBankStatementLine.SetSource(bankStmtLines);
        }

        private void localMenu_OnItemClicked(string ActionType)
        {
            var selectedItem = getBSLSelecteditem();
            dgAccountsTransGrid.tableView.CloseEditor();
            dgBankStatementLine.tableView.CloseEditor();
            switch (ActionType)
            {
                case "Save":
                    saveGrid();
                    break;
                case "Pre":
                    SetFilter(-1);
                    break;
                case "Next":
                    SetFilter(1);
                    break;
                case "Interval":
                    setInterval();
                    break;
                case "RefVoucher":
                    if (selectedItem == null)
                        return;
                    var source = (IList)dgBankStatementLine.ItemsSource;
                    if (source != null)
                    {
                        var _refferedVouchers = new List<int>();
                        IEnumerable<BankStatementLineGridClient> gridItems = source.Cast<BankStatementLineGridClient>();
                        foreach (var statementLine in gridItems)
                            if (statementLine._DocumentRef != 0)
                                _refferedVouchers.Add(statementLine._DocumentRef);

                        AddDockItem(TabControls.AttachVoucherGridPage, new object[1] { _refferedVouchers }, true);
                    }
                    
                    break;
                case "ViewVoucher":
                    bool useGLTrans = true;
                    if (selectedItem != null)
                    {
                        var actTransSelected = dgAccountsTransGrid.SelectedItem as GLTrans;
                        if (actTransSelected == null || actTransSelected._DocumentRef == 0)
                            useGLTrans = false;
                        else if (selectedItem._DocumentRef != 0 && selectedItem._DocumentRef != actTransSelected._DocumentRef)
                        {
                            var page = this as GridBasePage;
                            useGLTrans = (page.CurrentKeyDownGrid == dgAccountsTransGrid);
                        }
                    }
                    if (useGLTrans)
                    {
                        var actTransSelected = dgAccountsTransGrid.SelectedItem as GLTrans;
                        if (actTransSelected != null)
                        {
                            dgAccountsTransGrid.syncEntity.Row = actTransSelected;
                            busyIndicator.IsBusy = true;
                            ViewVoucher(TabControls.VouchersPage3, dgAccountsTransGrid.syncEntity);
                            busyIndicator.IsBusy = false;
                        }
                    }
                    else if(selectedItem != null)
                    {
                        dgBankStatementLine.syncEntity.Row = selectedItem;
                        busyIndicator.IsBusy = true;
                        ViewVoucher(TabControls.VouchersPage3, dgBankStatementLine.syncEntity);
                        busyIndicator.IsBusy = false;
                    }
                    break;

                case "ImportVoucher":
                    CWAddVouchers addVouvhersDialog = new CWAddVouchers(api, true);
                    addVouvhersDialog.Closing += delegate
                     {
                         if (addVouvhersDialog.DialogResult == true)
                         {
                             if (selectedItem == null) return;

                             if (addVouvhersDialog.VoucherRowIds != null && addVouvhersDialog.VoucherRowIds.Length > 0)
                             {
                                 dgBankStatementLine.SetLoadedRow(dgBankStatementLine.SelectedItem);
                                 selectedItem.VoucherReference = addVouvhersDialog.VoucherRowIds[0];
                                 dgBankStatementLine.SetLoadedRow(dgBankStatementLine.SelectedItem);
                             }
                         }
                     };
                    addVouvhersDialog.Show();
                    break;

                case "RemoveVoucher":
                    if (selectedItem == null)
                        return;
                    if (selectedItem._DocumentRef != 0)
                        selectedItem.VoucherReference = 0;
                    else
                        UnicontaMessageBox.Show(Uniconta.ClientTools.Localization.lookup("NoVoucherExist"), Uniconta.ClientTools.Localization.lookup("Information"), MessageBoxButton.OK);
                    break;
                case "VoucherTransactions":
                    var selected = dgAccountsTransGrid.SelectedItem as GLTransClientTotalBank;
                    if (selected == null)
                        return;
                    string vheader = string.Format("{0} ({1})", Uniconta.ClientTools.Localization.lookup("VoucherTransactions"), selected._Voucher);
                    AddDockItem(TabControls.AccountsTransaction, dgAccountsTransGrid.syncEntity, vheader);
                    break;
                case "AddMatch":
                    AddMatch();
                    break;
                case "RemoveMatch":
                    RemoveMatch();
                    break;
                case "TransferBankStatement":
                    saveGrid();
                    TransferBankStatementToJournal();
                    break;
                case "VoidTransaction":
                    var selectedTrans = dgAccountsTransGrid.SelectedItem as GLTransClientTotalBank;
                    if (selectedTrans != null)
                        ChangeVoidState(selectedTrans);
                    break;
                case "MarkForMatch":
                    Mark.Visible = MarkCol.Visible = !Mark.Visible;
                    break;
                case "ChangeOrientation":
                    orient = 1 - orient;
                    lGroup.Orientation = orient;
                    api.session.Preference.BankStatementHorisontal = (orient == Orientation.Horizontal);
                    break;
                case "ShowHideGreenLines":
                    hideGreen = !hideGreen;
                    setShowHideGreen(hideGreen);
                    string filterString = dgBankStatementLine.FilterString;
                    if (filterString.Contains("[State]"))
                        filterString = "";
                    else
                        filterString = "[State]<2";
                    dgBankStatementLine.FilterString = filterString;
                    dgAccountsTransGrid.FilterString = filterString;
                    break;
                case "ShowAmount":
                    ShowAmountWindow();
                    break;
                case "AutoMatch":
                    AutoMatch();
                    break;
                case "AddMapping":
                    if (selectedItem != null)
                    {
                        var bankImportMap = new CWAddBankImportMapping(api, master, selectedItem);
                        bankImportMap.Show();
                    }
                    break;
                case "OffSetAccount":
                    CallOffsetAccount(selectedItem);
                    break;
                default:
                    gridRibbon_BaseActions(ActionType);
                    break;
            }
        }

        public override void Utility_Refresh(string screenName, object argument = null)
        {
            if(screenName == TabControls.AttachVoucherGridPage && argument !=null)
            {
                var voucherObj = argument as object[];

                if(voucherObj[0] is VouchersClient)
                {
                    var voucher = voucherObj[0] as VouchersClient;
                    var selectedItem = dgBankStatementLine.SelectedItem as BankStatementLineGridClient;

                    if(selectedItem!=null && voucher.RowId!=0)
                    {
                        dgBankStatementLine.SetLoadedRow(selectedItem);
                        selectedItem.VoucherReference = voucher.RowId;
                        if (voucher._Invoice != 0)
                            selectedItem.Invoice = voucher._Invoice;
                        dgBankStatementLine.SetModifiedRow(selectedItem);
                    }
                }
            }
        }
        private void ShowAmountWindow()
        {
            CWShowAmount winShowAmount = new CWShowAmount();
            winShowAmount.Closing += delegate
            {
                if (winShowAmount.DialogResult == true)
                {
                    showAmountType = winShowAmount.AmountTypeOption;
                    InitQuery();
                }
            };
            winShowAmount.Show();
        }

        ItemBase ibase;
        bool hideGreen = false;
        void GetShowHideGreenMenuItem()
        {
            RibbonBase rb = (RibbonBase)localMenu.DataContext;
            ibase = UtilDisplay.GetMenuCommandByName(rb, "ShowHideGreenLines");
        }
        private void setShowHideGreen(bool hideGreen)
        {
            if (ibase == null)
                return;
            if (hideGreen)
            {
                ibase.Caption = string.Format(Uniconta.ClientTools.Localization.lookup("ShowOBJ"), Uniconta.ClientTools.Localization.lookup("Green"));
                ibase.LargeGlyph = Utilities.Utility.GetGlyph(";component/Assets/img/ShowGreen_32x32.png");
            }
            else
            {
                ibase.Caption = string.Format(Uniconta.ClientTools.Localization.lookup("HideOBJ"), Uniconta.ClientTools.Localization.lookup("Green"));
                ibase.LargeGlyph = Utilities.Utility.GetGlyph(";component/Assets/img/HideGreen_32x32.png");
            }
        }
        async void ChangeVoidState(GLTransClientTotalBank trans)
        {
            PostingAPI pApi = new PostingAPI(api);
            ErrorCodes res = await pApi.ChangeVoidState(trans);
            if (res == ErrorCodes.Succes)
            {
                trans._Void = !trans._Void;
                trans.UpdateVoid();
                UnicontaMessageBox.Show(Uniconta.ClientTools.Localization.lookup(trans._Void ? "TransactionWasReconciled" : "Unreconciled"), Uniconta.ClientTools.Localization.lookup("Message"));
            }
            else
                UtilDisplay.ShowErrorCode(res);
        }

        void TransferBankStatementToJournal()
        {
            CWTransferBankStatement winTransfer = new CWTransferBankStatement(fromDate, toDate, api, master);
#if !SILVERLIGHT
            winTransfer.DialogTableId = 2000000016;
#endif
            winTransfer.Closed += async delegate
            {
                if (winTransfer.DialogResult == true)
                {
                    master._Journal = winTransfer.Journal;
                    PostingAPI pApi = new PostingAPI(api);
                    var res = await pApi.TransferBankStatementToJournal(master, winTransfer.FromDate, winTransfer.ToDate, winTransfer.BankAsOffset, winTransfer.isMarkLine,winTransfer.AddVoucherNumber);
                    if (res == ErrorCodes.Succes)
                    {
                        string strmsg = string.Format("{0}; {1}! {2} ?", Uniconta.ClientTools.Localization.lookup("GenerateJournalLines"), Uniconta.ClientTools.Localization.lookup("Completed"),
                            string.Format(Uniconta.ClientTools.Localization.lookup("GoTo"), Uniconta.ClientTools.Localization.lookup("JournalLines"))
                            );
                        var select = UnicontaMessageBox.Show(strmsg, Uniconta.ClientTools.Localization.lookup("Information"), MessageBoxButton.OKCancel);
                        if (select == MessageBoxResult.OK)
                        {
                            var jour = new GLDailyJournalClient();
                            jour._Journal = winTransfer.Journal;
                            var err = await api.Read(jour);
                            if (err == ErrorCodes.Succes)
                                AddDockItem(TabControls.GL_DailyJournalLine, jour, null, null, true);
                        }
                    }
                    else
                        UtilDisplay.ShowErrorCode(res);

                    InitQuery();
                }
            };
            winTransfer.Show();

        }
        static void Join(BankStatementLineGridClient bst, GLTransClientTotalBank act, bool HighlightLine = false)
        {
            bst._JournalPostedId = act._JournalPostedId;
            bst.Voucher = act._Voucher;
            bst.VoucherLine = act._VoucherLine;
            bst.PostedDate = act._Date;
            bst.TransType = act._TransType;
            bst._DocumentRef = act._DocumentRef;
            bst.AccountType = act.DCType;
            bst.Account = act._DCAccount;
            if (HighlightLine)
                bst.IsMatched = true;
            else
            {
                bst._IsMatched = true;
                bst.UpdateState();
            }
            bst.Updated = true;
            bst.Mark = false;
        }

        void AutoMatch()
        {
            if (dgBankStatementLine.ItemsSource == null || dgAccountsTransGrid.ItemsSource == null)
                return;
            var days = DaysSlip;
            var cnt = AutoMatchOne2One(days);
            cnt += AutoMatchOne2Many(days);
            cnt += AutoMatchMany2One(days);
            UnicontaMessageBox.Show(string.Format("{0}: {1}", Uniconta.ClientTools.Localization.lookup("NumberOfRecords"), cnt), Uniconta.ClientTools.Localization.lookup("Message"));
        }

        int AutoMatchOne2One(int slip)
        {
            int cnt = 0;
            var lstbsl = ((IEnumerable<BankStatementLineGridClient>)dgBankStatementLine.ItemsSource);
            var bstList = lstbsl.Where(bl => bl.Mark == false && bl.State == (byte)1).ToList();
            var lstAct = ((IEnumerable<GLTransClientTotalBank>)dgAccountsTransGrid.ItemsSource);
            var actList = lstAct.Where(ac => ac.Mark == false && ac.State == (byte)1).ToList();

            for (int OffsetDays = 0; (OffsetDays <= slip); OffsetDays++)
            {
                foreach (var bst in bstList)
                {
                    if (bst.State != 1)
                        continue;

                    var MinDate = bst.Date;
                    var MaxDate = MinDate;
                    if (OffsetDays > 0)
                    {
                        MinDate = MinDate.AddDays(-OffsetDays);
                        MaxDate = MaxDate.AddDays(OffsetDays);
                    }
                    var Amount = bst._AmountCent;
                    foreach (var act in actList)
                    {
                        if (act._Date > MaxDate)
                            break;

                        var TransAcmount = ShowCurrency ? act._AmountCurCent : act._AmountCent;
                        if (Amount == TransAcmount && act._Date >= MinDate && act._Date <= MaxDate && act.State == 1)
                        {
                            Join(bst, act);
                            bst.Trans = new List<GLTransClientTotalBank>() { act };
                            act._Reconciled = true;
                            act.Mark = false;
                            act._IsMatched = true;
                            act.StatementLines = new List<BankStatementLineGridClient>() { bst };
                            act.UpdateState();
                            cnt++;
                            break;
                        }
                    }
                }
            }
            return cnt;
        }

        int AutoMatchOne2Many(int slip)
        {
            int cnt = 0;
            var lstbsl = ((IEnumerable<BankStatementLineGridClient>)dgBankStatementLine.ItemsSource);
            var bstList = lstbsl.Where(bl => bl.Mark == false && bl.State == (byte)1).ToList();
            var lstAct = ((IEnumerable<GLTransClientTotalBank>)dgAccountsTransGrid.ItemsSource);
            var actList = lstAct.Where(ac => ac.Mark == false && ac.State == (byte)1).ToArray();
            int actlen = actList.Length;

            for (int OffsetDays = 0; (OffsetDays <= slip); OffsetDays++)
            {
                foreach (var bst in bstList)
                {
                    if (bst.State != 1)
                        continue;

                    var MinDate = bst.Date;
                    var MaxDate = MinDate;
                    if (OffsetDays > 0)
                    {
                        MinDate = MinDate.AddDays(-OffsetDays);
                        MaxDate = MaxDate.AddDays(OffsetDays);
                    }
                    var Amount = bst._AmountCent;

                    int startIdx = 0;
                    while (startIdx < actlen && actList[startIdx]._Date < MinDate)
                        startIdx++;

                    while (startIdx < actlen)
                    {
                        var act = actList[startIdx];
                        if (act._Date > MaxDate)
                            break;

                        long sumTrans = 0;
                        int n = startIdx;
                        while (n < actlen)
                        {
                            var act2 = actList[n++];
                            if (act2._Date > MaxDate || act2.State != 1)
                                break;

                            var newAmount = ShowCurrency ? act2._AmountCurCent : act2._AmountCent;
                            if ((sumTrans > 0 && newAmount < 0) || (sumTrans < 0 && newAmount > 0))
                                break;
                            sumTrans += newAmount;
                            if (sumTrans == Amount)
                            {
                                var bstLink = new List<BankStatementLineGridClient>() { bst };
                                Join(bst, act);
                                bst.Trans = new List<GLTransClientTotalBank>();
                                for (int i = startIdx; (i < n); i++)
                                {
                                    act = actList[i];
                                    bst.Trans.Add(act);
                                    act._Reconciled = true;
                                    act.Mark = false;
                                    act._IsMatched = true;
                                    act.StatementLines = bstLink;
                                    act.UpdateState();
                                    cnt++;
                                }
                                startIdx = actlen; // we will exit
                                break;
                            }
                            if (Math.Abs(sumTrans) > Math.Abs(Amount))
                                break;
                        }
                        startIdx++;
                    }
                }
            }
            return cnt;
        }

        int AutoMatchMany2One(int slip)
        {
            int cnt = 0;
            var lstbsl = ((IEnumerable<BankStatementLineGridClient>)dgBankStatementLine.ItemsSource);
            var bstList = lstbsl.Where(bl => bl.Mark == false && bl.State == (byte)1).ToArray();
            int bstlen = bstList.Length;
            var lstAct = ((IEnumerable<GLTransClientTotalBank>)dgAccountsTransGrid.ItemsSource);
            var actList = lstAct.Where(ac => ac.Mark == false && ac.State == (byte)1).ToList();

            for (int OffsetDays = 0; (OffsetDays <= slip); OffsetDays++)
            {
                foreach (var act in lstAct)
                {
                    if (act.State != 1)
                        continue;

                    var MinDate = act.Date;
                    var MaxDate = MinDate;
                    if (OffsetDays > 0)
                    {
                        MinDate = MinDate.AddDays(-OffsetDays);
                        MaxDate = MaxDate.AddDays(OffsetDays);
                    }
                    var Amount = ShowCurrency ? act._AmountCurCent : act._AmountCent;

                    int startIdx = 0;
                    while (startIdx < bstlen && bstList[startIdx]._Date < MinDate)
                        startIdx++;

                    while (startIdx < bstlen)
                    {
                        var bst = bstList[startIdx];
                        if (bst._Date > MaxDate)
                            break;

                        long sumTrans = 0;
                        int n = startIdx;
                        while (n < bstlen)
                        {
                            var bst2 = bstList[n++];
                            if (bst2._Date > MaxDate || bst2.State != 1)
                                break;

                            var newAmount = bst2._AmountCent;
                            if ((sumTrans > 0 && newAmount < 0) || (sumTrans < 0 && newAmount > 0))
                                break;
                            sumTrans += newAmount;
                            if (sumTrans == Amount)
                            {
                                var actLink = new List<GLTransClientTotalBank>() { act };
                                act._Reconciled = true;
                                act.Mark = false;
                                act._IsMatched = true;
                                act.UpdateState();
                                act.StatementLines = new List<BankStatementLineGridClient>();
                                for (int i = startIdx; (i < n); i++)
                                {
                                    bst = bstList[i];
                                    act.StatementLines.Add(bst);
                                    Join(bst, act);
                                    bst.Trans = actLink;
                                    cnt++;
                                }
                                startIdx = bstlen; // we will exit
                                break;
                            }
                            if (Math.Abs(sumTrans) > Math.Abs(Amount))
                                break;
                        }
                        startIdx++;
                    }
                }
            }
            return cnt;
        }

        bool AddMatch()
        {
            var lstbsl = ((IEnumerable<BankStatementLineGridClient>)dgBankStatementLine.ItemsSource);
            if (lstbsl == null)
                return false;
            var lstAct = ((IEnumerable<GLTransClientTotalBank>)dgAccountsTransGrid.ItemsSource);
            if (lstAct == null)
                return false;
            var markedbstList = lstbsl.Where(bl => bl.Mark == true && bl.State == (byte)1).ToList();
            var markedactList = lstAct.Where(ac => ac.Mark == true && ac.State == (byte)1).ToList();
            if (!markedbstList.Any() && !markedactList.Any())
            {
                /* take selected item */
                var bl = getBSLSelecteditem();
                if (bl != null && bl.State == (byte)1)
                    markedbstList.Add(bl);
                var ac = dgAccountsTransGrid.CurrentItem as GLTransClientTotalBank;
                if (ac != null && ac.State == (byte)1)
                    markedactList = new List<GLTransClientTotalBank>() { ac };
            }
            /*
            if (markedbstList.Count > 1 && markedactList.Count > 1)
            {
                UnicontaMessageBox.Show(Uniconta.ClientTools.Localization.lookup("TooManyLinesSelected"), Uniconta.ClientTools.Localization.lookup("Information"), MessageBoxButton.OK);
                return false;
            }
            */
            var ShowCurrency = this.ShowCurrency;

            if (markedbstList.Any() && markedactList.Any())
            {
                long sum1 = 0;
                foreach (var rec in markedbstList)
                    sum1 += rec._AmountCent;

                long sum2 = 0;
                foreach (var rec in markedactList)
                    if (ShowCurrency)
                        sum2 += rec._AmountCurCent;
                    else
                        sum2 += rec._AmountCent;

                if (Math.Abs(sum1 - sum2) > this.master._AllowedDifInMatch * 100d)
                {
                    UnicontaMessageBox.Show(string.Format("{0}. {1}={2} {3}={4}", Uniconta.ClientTools.Localization.lookup("ReconcileSumErr"),
                        Uniconta.ClientTools.Localization.lookup("BankStatement"), (sum1 / 100d).ToString("N2"),
                        Uniconta.ClientTools.Localization.lookup("Transactions"), (sum2 / 100d).ToString("N2")),
                        Uniconta.ClientTools.Localization.lookup("Error"), MessageBoxButton.OK);
                    return false;
                }

                if (markedbstList.Count == 1)
                {
                    var bst = markedbstList.First();
                    bst.Updated = true;
                    bst.Mark = false;
                    bst.Trans = markedactList;
                    bool first = true;
                    foreach (var act in markedactList)
                    {
                        if (first)
                        {
                            first = false;
                            Join(bst, act, true);
                        }
                        act.StatementLines = markedbstList;
                        act._Reconciled = true;
                        act.Mark = false;
                        act.IsMatched = true;
                    }
                }
                else if (markedactList.Count == 1)
                {
                    var act = markedactList.First();
                    act._Reconciled = true;
                    act.Mark = false;
                    act.IsMatched = true;
                    act.StatementLines = markedbstList;
                    foreach (var bst in markedbstList)
                    {
                        Join(bst, act, true);
                        bst.Trans = markedactList;
                    }
                }
                else
                {
                    bool first = true;
                    foreach (var bst in markedbstList)
                    {
                        bst.Updated = true;
                        bst.Mark = false;
                        bst.Trans = markedactList;
                        foreach (var act in markedactList)
                        {
                            if (first)
                            {
                                first = false;
                                Join(bst, act, true);
                            }
                            act.StatementLines = markedbstList;
                            act._Reconciled = true;
                            act.Mark = false;
                            act.IsMatched = true;
                        }
                    }
                }
            }
            return true;
        }

        void RemoveMatch()
        {
            var lstbsl = ((IEnumerable<BankStatementLineGridClient>)dgBankStatementLine.ItemsSource);
            var markedbstList = lstbsl.Where(bl => bl.Mark == true && bl.State != (byte)1).ToList();
            if (!markedbstList.Any())
            {
                /* take selected item */
                var bt = getBSLSelecteditem();
                if (bt != null)
                    markedbstList.Add(bt);
            }
            if (markedbstList.Count == 1)
            {
                var bt = markedbstList.First();
                if (bt._Trans != null && bt._Trans.Count == 1)
                {
                    var ac = bt._Trans.First();
                    if (ac.StatementLines.Count > 1)
                    {
                        ac.IsCleared = true;
                        markedbstList = ac.StatementLines;
                    }
                }
            }

            foreach (var bst in markedbstList)
            {
                bst.Updated = true;
                bst._JournalPostedId = 0;
                bst.Voucher = 0;
                bst.VoucherLine = 0;
                bst.PostedDate = DateTime.MinValue;
                bst.TransType = null;
                bst._DocumentRef = 0;
                bst.AccountType = null;
                bst.Account = null;
                if (bst._Trans != null)
                {
                    bst._Trans.ForEach(tr => { tr.StatementLines = null; tr.Mark = false; tr._Reconciled = false; tr.IsMatched = false; });  // remove point back
                    bst._Trans = null;
                    bst.UpdateState();
                }
                bst.IsMatched = false;
            }
        }

        void setInterval()
        {
            CWInterval winInterval = new CWInterval(fromDate, toDate, DaysSlip);
            winInterval.Closed += delegate
            {
                if (winInterval.DialogResult == true)
                {
                    fromDate = winInterval.FromDate;
                    toDate = winInterval.ToDate;
                    DaysSlip = winInterval.VarianceDays;
                    SetStatusText();
                    saveGrid(true);
                }
            };
            winInterval.Show();
        }

        void SetFilter(int step)
        {
            showAmountType = Uniconta.ClientTools.Localization.lookup("All");
            fromDate = fromDate.AddMonths(step);
            var td = toDate.AddMonths(step);
            toDate = td.AddDays(DateTime.DaysInMonth(td.Year, td.Month) - td.Day);
            SetStatusText();
            saveGrid(true);
        }

        async void saveGrid(bool doReload = false)
        {
            if (getBSLSelecteditem() != null)
                dgBankStatementLine.SelectedItem = null;

            if (doReload)
                busyIndicator.IsBusy = true;

            var err = await dgBankStatementLine.SaveData();
            if (err != ErrorCodes.Succes)
                doReload = false;
            if (BankList == null)
                return;
            List<BankStatementAPI.BankSettle> multiSettleTrans = null;
            List<BankStatementAPI.TransSettle> multiSettleBank = null;
            foreach (var bank in BankList)
            {
                if (!bank.Updated)
                    continue;

                bank.Updated = false;

                var _Trans = bank._Trans;
                if (_Trans != null && _Trans.Count == 1 && _Trans.First().StatementLines.Count > 1)
                {
                    if (multiSettleBank == null)
                        multiSettleBank = new List<BankStatementAPI.TransSettle>();

                    BankStatementAPI.TransSettle bs = new BankStatementAPI.TransSettle();
                    var t = _Trans.First();
                    if (t.IsCleared)
                    {
                        t.IsCleared = false;
                        bs.TransLine = t;
                        multiSettleBank.Add(bs);

                        bs = new BankStatementAPI.TransSettle();
                    }

                    bs.TransLine = t;
                    bs.BankLines = t.StatementLines;
                    t.StatementLines.ForEach(tr => tr.Updated = false);
                    multiSettleBank.Add(bs);
                }
                else
                {
                    if (multiSettleTrans == null)
                        multiSettleTrans = new List<BankStatementAPI.BankSettle>();

                    BankStatementAPI.BankSettle bp = new BankStatementAPI.BankSettle();
                    bp.BankLine = bank;
                    bp.TransLines = _Trans;
                    multiSettleTrans.Add(bp);

                    if (_Trans != null)
                    {
                        foreach (var t in _Trans)
                        {
                            if (t.IsCleared)
                            {
                                t.IsCleared = false;
                                var bs = new BankStatementAPI.TransSettle();
                                bs.TransLine = t;
                                if (multiSettleBank == null)
                                    multiSettleBank = new List<BankStatementAPI.TransSettle>();
                                multiSettleBank.Add(bs);
                            }
                        }
                    }
                }
            }
            if (multiSettleTrans != null || multiSettleBank != null)
            {
                var bapi = new BankStatementAPI(api);

                err = 0;
                if (multiSettleBank != null)
                    err = await bapi.Settle(this.master, multiSettleBank);
                if (err == 0 && multiSettleTrans != null)
                    err = await bapi.Settle(this.master, multiSettleTrans);
                if (err != 0)
                {
                    if (doReload)
                        busyIndicator.IsBusy = false;
                    UtilDisplay.ShowErrorCode(err);
                    return;
                }
            }

            if (doReload)
            {
                dgAccountsTransGrid.SetSource(null);
                dgBankStatementLine.SetSource(null);
                InitQuery();
            }
        }

        BankStatementLineGridClient getBSLSelecteditem()
        {
            return dgBankStatementLine.SelectedItem as BankStatementLineGridClient;
        }
        void SetStatusText()
        {
            RibbonBase rb = (RibbonBase)localMenu.DataContext;
            var groups = UtilDisplay.GetMenuCommandsByStatus(rb, true);

            foreach (var grp in groups)
            {
                if (grp.StatusValue == "a" || grp.Caption == Uniconta.ClientTools.Localization.lookup("FromDate"))
                {
                    grp.StatusValue = fromDate.ToString("d");
                    continue;
                }
                else
                if (grp.StatusValue == "b" || grp.Caption == Uniconta.ClientTools.Localization.lookup("ToDate"))
                {
                    grp.StatusValue = toDate.ToString("d");
                }
            }

        }
        void setDim()
        {
            UnicontaClient.Utilities.Utility.SetDimensionsGrid(api, cldim1, cldim2, cldim3, cldim4, cldim5);
            UnicontaClient.Utilities.Utility.SetDimensionsGrid(api, coldim1, coldim2, coldim3, coldim4, coldim5);
        }

        List<GLTransClientTotalBank> lastSelected;
        private void dgBankStatementLine_SelectedItemChanged(object sender, DevExpress.Xpf.Grid.SelectedItemChangedEventArgs e)
        {
            var selectedbsl = getBSLSelecteditem();
            var oldBsl = e.OldItem as BankStatementLineGridClient;
            if (oldBsl != null)
                oldBsl.IsMatched = false;
            lastSelected?.ForEach(ac => { ac.IsMatched = false; ac.UpdateState(); });
            if (selectedbsl == null)
                return;
            var trans = selectedbsl.Trans;
            if (trans != null && trans.Count > 0)
            {
                dgAccountsTransGrid.CurrentItem = trans[0];
                trans.ForEach(ac => { ac.IsMatched = true; ac.UpdateState(); });
                lastSelected = trans;
            }
            else
            {
                var selectedtrans = dgAccountsTransGrid.CurrentItem as GLTransClientTotalBank;
                if (selectedtrans != null && selectedtrans.StatementLines != null)
                    dgAccountsTransGrid.CurrentItem = null;
            }
            dgBankStatementLine.Readonly = !selectedbsl.AllowEditing;
            MarkCol.ReadOnly = false;
            //  MarkCol.AllowEditing = true;
            var ae = dgBankStatementLine.tableView.ActiveEditor;
            if (ae != null)
                ae.IsEnabled = selectedbsl.AllowEditing;
        }

        List<BankStatementLineGridClient> lastSelectedbsl;

        private void CheckEditor_Checked(object sender, RoutedEventArgs e)
        {
            BankStatementClientSetMark(true);
        }

        private void CheckEditor_Unchecked(object sender, RoutedEventArgs e)
        {
            BankStatementClientSetMark(false);
        }

        void BankStatementClientSetMark(bool value)
        {
            var visibleRows = dgBankStatementLine.GetVisibleRows() as IEnumerable<BankStatementLineGridClient>;
            foreach (var row in visibleRows)
            {
                row.Mark = value;
            }
            var val = (from t in visibleRows where t._Mark select t._AmountCent).Sum();
            this.layOutBankStat.Caption = string.Format("{0}  '{1:N2}'", bankStatCaption, (val / 100d));
        }

        private void CheckEditor_Checked_1(object sender, RoutedEventArgs e)
        {
            GLTransClientTotalSetMark(true);
        }

        private void CheckEditor_Unchecked_1(object sender, RoutedEventArgs e)
        {
            GLTransClientTotalSetMark(false);
        }

        void GLTransClientTotalSetMark(bool value)
        {
            var visibleRows = dgAccountsTransGrid.GetVisibleRows() as IEnumerable<GLTransClientTotalBank>;
            foreach (var row in visibleRows)
                row.Mark = value;
            long val;
            if (ShowCurrency)
                val = (from t in visibleRows where t._Mark select t._AmountCurCent).Sum();
            else
                val = (from t in visibleRows where t._Mark select t._AmountCent).Sum();
            this.layOutTrans.Caption = string.Format("{0}  '{1:N2}'", transactionCaption, (val / 100d));

        }

        private void dgAccountsTransGrid_SelectedItemChanged(object sender, SelectedItemChangedEventArgs e)
        {
            var selectedTrans = dgAccountsTransGrid.CurrentItem as GLTransClientTotalBank;
            var oldTrans = e.OldItem as GLTransClientTotalBank;
            if (oldTrans != null)
                oldTrans.IsMatched = false;
            if (selectedTrans == null)
            {
                lastSelectedbsl?.ForEach(b => b.IsMatched = false);
                return;
            }
            var stline = selectedTrans.StatementLines;
            lastSelectedbsl?.ForEach(b => b.IsMatched = false);
            if (stline != null && stline.Count > 0)
            {
                dgBankStatementLine.CurrentItem = stline[0];
                stline.ForEach(b => b.IsMatched = true);
                lastSelectedbsl = stline;
            }
            else
            {
                var bsl = getBSLSelecteditem();
                if (bsl != null && bsl._Trans != null)
                    dgBankStatementLine.CurrentItem = null;
            }
        }

        public override bool IsDataChaged
        {
            get
            {
                var lstbsl = ((IEnumerable<BankStatementLineGridClient>)dgBankStatementLine.ItemsSource);
                if (lstbsl != null && lstbsl.Any(l => l.Updated))
                    return true;
                return base.IsDataChaged;
            }
        }

        private void HasOffSetAccount_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CallOffsetAccount((sender as Image).Tag as BankStatementLineGridClient);
        }

        void CallOffsetAccount(BankStatementLineGridClient line)
        {
            if (line != null)
            {
                dgBankStatementLine.SetLoadedRow(line);
                var header = string.Format("{0}:{1} {2}", Uniconta.ClientTools.Localization.lookup("OffsetAccountTemplate"), Uniconta.ClientTools.Localization.lookup("BankStatement"), line._Account);
                AddDockItem(TabControls.GLOffsetAccountTemplate, line, header: header);
            }
        }
    }

    public class BankStatementLineGridClient : BankStatementLineClient
    {
        internal int AccountVersion;
        internal object accntSource;
        public object AccountSource { get { return accntSource; } }

        internal List<GLTransClientTotalBank> _Trans;
        internal List<GLTransClientTotalBank> Trans { get { return _Trans; } set { _Trans = value; NotifyPropertyChanged("State"); } }

        internal bool Updated;
        public void UpdateState() { NotifyPropertyChanged("State"); }

        public long _Total;

        [Display(Name = "Total", ResourceType = typeof(GLDailyJournalText))]
        public double Total { get { return _Total / 100d; } }

        [Display(Name = "Void", ResourceType = typeof(GLDailyJournalLineText))]
        public bool VoidLine { get { return _Void; } set { _Void = value; NotifyPropertyChanged("VoidLine"); NotifyPropertyChanged("State"); } }

        internal bool _IsMatched;
        public bool IsMatched { get { return _IsMatched; } set { _IsMatched = value; NotifyPropertyChanged("IsMatched"); NotifyPropertyChanged("State"); NotifyPropertyChanged("AllowEditing"); } }
        public bool AllowEditing { get { return State == 1 || _Void; } }
        public byte State { get { return _Trans != null || _Primo || this._JournalPostedId != 0 ? (byte)3 : _Void ? (byte)4 : (_InJournal ? (byte)2 : (byte)1); } }
        internal bool _Mark;
        public bool Mark { get { return _Mark; } set { if (_Mark == value) return; _Mark = value; NotifyPropertyChanged("Mark"); } }

        public bool HasOffSetAccounts { get { return _HasOffsetAccounts; } }
    }

    public class GLTransClientTotalBank : GLTransClientTotal, INotifyPropertyChanged
    {
        internal List<BankStatementLineGridClient> _StatementLines;
        public List<BankStatementLineGridClient> StatementLines { get { return _StatementLines; } set { _StatementLines = value; NotifyPropertyChanged("State"); } }
        public byte State { get { return _StatementLines != null || _Reconciled || _PrimoTrans ? (byte)3 : _Void ? (byte)4 : (byte)1; } }
        public bool AllowEditing { get { return State == 1 || _Void; } }
        internal bool _IsMatched;
        public bool IsMatched { get { return _IsMatched; } set { _IsMatched = value; NotifyPropertyChanged("IsMatched"); NotifyPropertyChanged("State"); NotifyPropertyChanged("AllowEditing"); } }
        internal bool _Mark;
        public bool Mark { get { return _Mark; } set { if (_Mark == value) return; _Mark = value; NotifyPropertyChanged("Mark"); } }
        internal bool IsCleared;

        public void UpdateState()
        {
            NotifyPropertyChanged("State");
        }
        public void UpdateVoid()
        {
            NotifyPropertyChanged("Void");
            UpdateState();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
