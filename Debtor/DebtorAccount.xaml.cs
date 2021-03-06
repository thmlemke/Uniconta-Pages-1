using Uniconta.API.Service;
using UnicontaClient.Models;
using UnicontaClient.Utilities;
using Uniconta.ClientTools;
using Uniconta.ClientTools.DataModel;
using Uniconta.ClientTools.Page;
using Uniconta.ClientTools.Util;
using Uniconta.Common;
using DevExpress.Xpf.Editors.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Collections;
using Uniconta.ClientTools.Controls;
using Uniconta.DataModel;

using UnicontaClient.Pages;
namespace UnicontaClient.Pages.CustomPage
{
    public class DebtorAccountGrid : CorasauDataGridClient
    {
        public override Type TableType { get { return typeof(DebtorClient); } }
        public override bool SingleBufferUpdate { get { return false; } }
    }
    public partial class DebtorAccount : GridBasePage
    {
        public override string NameOfControl
        {
            get { return TabControls.DebtorAccount.ToString(); }
        }
        public DebtorAccount(BaseAPI API) : base(API, string.Empty)
        {
            InitPage();
        }
        public DebtorAccount(BaseAPI api, string lookupKey)
            : base(api, lookupKey)
        {
            InitPage();
        }

        private void InitPage()
        {
            InitializeComponent();
            dgDebtorAccountGrid.RowDoubleClick += dgDebtorAccountGrid_RowDoubleClick;
            localMenu.dataGrid = dgDebtorAccountGrid;
            LayoutControl = detailControl.layoutItems;
            dgDebtorAccountGrid.api = api;
            dgDebtorAccountGrid.BusyIndicator = busyIndicator;
            SetRibbonControl(localMenu, dgDebtorAccountGrid);

            localMenu.OnItemClicked += localMenu_OnItemClicked;
            ribbonControl.DisableButtons(new string[] { "AddLine", "CopyRow", "DeleteRow", "UndoDelete", "SaveGrid" });

            var Comp = api.CompanyEntity;
            if (Comp.RoundTo100)
                CurBalance.HasDecimals = Overdue.HasDecimals = false;
            if (Comp.CRM)
                LoadType(new Type[] { typeof(Uniconta.DataModel.DebtorGroup), typeof(Uniconta.DataModel.CrmInterest), typeof(Uniconta.DataModel.CrmProduct) });
            else
                LoadNow(typeof(Uniconta.DataModel.DebtorGroup));

            dgDebtorAccountGrid.ShowTotalSummary();

#if SILVERLIGHT
            Application.Current.RootVisual.KeyDown += RootVisual_KeyDown;
#else
            this.PreviewKeyDown += RootVisual_KeyDown;
#endif
            this.BeforeClose += DebtorAccount_BeforeClose;
        }

        private void RootVisual_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F8 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                localMenu_OnItemClicked("DebtorTran");
            }
            else if (e.Key == Key.F8)
            {
                localMenu_OnItemClicked("OpenTran");
            }
        }

        private void DebtorAccount_BeforeClose()
        {
#if SILVERLIGHT
            Application.Current.RootVisual.KeyDown -= RootVisual_KeyDown;
#else
            this.PreviewKeyDown -= RootVisual_KeyDown;
#endif
        }

        public override Task InitQuery()
        {
            if (!this.dgDebtorAccountGrid.ReuseCache(typeof(Uniconta.DataModel.Debtor)))
                return BindGrid();
            return null;
        }
        protected override void OnLayoutCtrlLoaded()
        {
            detailControl.api = api;
        }
        protected override void OnLayoutLoaded()
        {
            base.OnLayoutLoaded();
            UnicontaClient.Utilities.Utility.SetDimensionsGrid(api, cldim1, cldim2, cldim3, cldim4, cldim5);

            var Comp = api.CompanyEntity;
            if (!Comp.CRM)
            {
                CrmGroup.Visible = false;
                Interests.Visible = false;
                Products.Visible = false;
            }
            if (!Comp.DeliveryAddress)
            {
                DeliveryName.Visible = false;
                DeliveryAddress1.Visible = false;
                DeliveryAddress2.Visible = false;
                DeliveryAddress3.Visible = false;
                DeliveryZipCode.Visible = false;
                DeliveryCity.Visible = false;
                DeliveryCountry.Visible = false;
            }
            if (!Comp._DirectDebit)
            {
                PaymentFormat.Visible = false;
            }
            dgDebtorAccountGrid.Readonly = true;
        }

        void dgDebtorAccountGrid_RowDoubleClick()
        {
            localMenu_OnItemClicked("DebtorTran");
        }

        private void localMenu_OnItemClicked(string ActionType)
        {
            var selectedItem = dgDebtorAccountGrid.SelectedItem as DebtorClient;
            var selectedItems = dgDebtorAccountGrid.SelectedItems;

            switch (ActionType)
            {
                case "EditAll":
                    if (dgDebtorAccountGrid.Visibility == Visibility.Visible)
                        EditAll();
                    break;
                case "AddRow":
                    object[] param = new object[2];
                    param[0] = api;
                    param[1] = null;
                    AddDockItem(TabControls.DebtorAccountPage2, param, Uniconta.ClientTools.Localization.lookup("DebtorAccount"), ";component/Assets/img/Add_16x16.png");
                    break;
                case "EditRow":
                    if (selectedItem != null)
                    {
                        object[] Params = new object[2] { selectedItem, true };
                        AddDockItem(TabControls.DebtorAccountPage2, Params, string.Format("{0}: {1}", Uniconta.ClientTools.Localization.lookup("DebtorAccount"), selectedItem._Account));
                    }
                    break;
                case "DebtorTran":
                    if (selectedItem != null)
                        AddDockItem(TabControls.DebtorTransactions, dgDebtorAccountGrid.syncEntity);
                    break;
                case "OpenTran":
                    if (selectedItem != null)
                        AddDockItem(TabControls.DebtorOpenTransactions, dgDebtorAccountGrid.syncEntity);
                    break;
                case "Contacts":
                    if (selectedItem != null)
                        AddDockItem(TabControls.ContactPage, dgDebtorAccountGrid.syncEntity);
                    break;
                case "ClientItemNumber":
                    if (selectedItem != null && selectedItem._ItemNameGroup != null)
                        AddDockItem(TabControls.LanguageItemTextPage, selectedItem);
                    else
                        UtilDisplay.ShowFieldMissing("ItemNameGroup");
                    break;
#if !SILVERLIGHT
                case "CreateMandates":
                    if (selectedItems != null)
                        CreateMandates(selectedItems);
                    break;
#endif
                case "Prices":
                    if (selectedItem != null && selectedItem._PriceList != null)
                        AddDockItem(TabControls.CustomerPriceListLinePage, selectedItem);
                    else
                        UtilDisplay.ShowFieldMissing("PriceList");
                    break;
                case "DebtorOrders":
                    if (selectedItem != null)
                        AddDockItem(TabControls.DebtorOrdersMultiple, dgDebtorAccountGrid.syncEntity);
                    break;
                case "FollowUp":
                    if (selectedItem != null)
                    {
                        var header = string.Format("{0}:{2} {1}", Uniconta.ClientTools.Localization.lookup("FollowUp"), selectedItem._Name, Uniconta.ClientTools.Localization.lookup("Accounts"));
                        AddDockItem(TabControls.CrmFollowUpPage, dgDebtorAccountGrid.syncEntity, header);
                    }
                    break;
                case "AddNote":
                    if (selectedItem != null)
                        AddDockItem(TabControls.UserNotesPage, dgDebtorAccountGrid.syncEntity);
                    break;
                case "AddDoc":
                    if (selectedItem != null)
                        AddDockItem(TabControls.UserDocsPage, dgDebtorAccountGrid.syncEntity, string.Format("{0}: {1}", Uniconta.ClientTools.Localization.lookup("Documents"), selectedItem._Name));
                    break;
                case "Orders":
                    if (dgDebtorAccountGrid.syncEntity == null)
                        return;
                    AddDockItem(TabControls.DebtorOrdersMultiple, dgDebtorAccountGrid.syncEntity);
                    break;
                case "Offers":
                    if (selectedItem != null)
                        AddDockItem(TabControls.DebtorOffers, dgDebtorAccountGrid.syncEntity);
                    break;
                case "Invoices":
                    if (selectedItem != null)
                    {
                        string header = string.Format("{0}/{1}", Uniconta.ClientTools.Localization.lookup("DebtorInvoice"), selectedItem._Account);
                        AddDockItem(TabControls.Invoices, dgDebtorAccountGrid.syncEntity, header);
                    }
                    break;
                case "InvTran":
                    if (selectedItem != null)
                        AddDockItem(TabControls.InvTransactions, dgDebtorAccountGrid.syncEntity);
                    break;
                case "AccountStat":
                    if (selectedItem != null)
                        AddDockItem(TabControls.InventoryTotals, selectedItem, string.Format("{0}:{1}", Uniconta.ClientTools.Localization.lookup("AccountStat"), selectedItem._Account));
                    break;
                case "AccountStatDate":
                    if (selectedItem == null)
                        return;
                    object[] obj = new object[2];
                    obj[0] = selectedItem;
                    obj[1] = true;
                    AddDockItem(TabControls.DebtorAccountStat, obj, string.Format("{0}:{1}", Uniconta.ClientTools.Localization.lookup("AccountStat"), selectedItem._Account));
                    break;
                case "RefreshGrid":
                    if (gridControl.Visibility == Visibility.Visible)
                        gridRibbon_BaseActions(ActionType);
                    break;
                case "AddLine":
                    dgDebtorAccountGrid.AddRow();
                    break;
                case "CopyRow":
                    if (copyRowIsEnabled)
                        dgDebtorAccountGrid.CopyRow();
                    else
                        CopyRecord(selectedItem);
                    break;
                case "DeleteRow":
                    dgDebtorAccountGrid.DeleteRow();
                    break;
                case "SaveGrid":
                    Save();
                    break;
                case "DebtorStatement":
                    if (selectedItem != null)
                        AddDockItem(TabControls.DebtorStatement, dgDebtorAccountGrid.syncEntity);
                    break;
                case "DeliveryAddresses":
                    if (selectedItem != null)
                    {
                        var header = string.Format("{0}: {1}, {2}", Uniconta.ClientTools.Localization.lookup("DeliveryAddresses"), selectedItem._Account, selectedItem._Name);
                        AddDockItem(TabControls.WorkInstallationPage, dgDebtorAccountGrid.syncEntity, header);
                    }
                    break;
                case "QuickInvoice":
                    if (selectedItem != null)
                        AddDockItem(TabControls.CreateInvoicePage, selectedItem);
                    break;
                case "CopyRecord":
                    CopyRecord(selectedItem);
                    break;
#if WPF
                case "DebtorTransPivot":
                    if (selectedItem != null)
                        AddDockItem(TabControls.DebtorInvoiceLinesPivotReport, selectedItem, string.Format("{0}: {1}", Uniconta.ClientTools.Localization.lookup("Pivot"), selectedItem._Name));
                    break;
#endif
                case "Project":
                    if (selectedItem != null)
                        AddDockItem(TabControls.Project, selectedItem, string.Format("{0}: {1}", Uniconta.ClientTools.Localization.lookup("Project"), selectedItem._Name));
                    break;
                case "UndoDelete":
                    dgDebtorAccountGrid.UndoDeleteRow();
                    break;
                default:
                    gridRibbon_BaseActions(ActionType);
                    break;
            }
        }

        void CopyRecord(DebtorClient selectedItem)
        {
            if (selectedItem == null)
                return;
            var debtor = Activator.CreateInstance(selectedItem.GetType()) as DebtorClient;
            StreamingManager.Copy(selectedItem, debtor);
            var parms = new object[2] { debtor, false };
            AddDockItem(TabControls.DebtorAccountPage2, parms, Uniconta.ClientTools.Localization.lookup("DebtorAccount"), ";component/Assets/img/Add_16x16.png");
        }

#if !SILVERLIGHT
        async void CreateMandates(IList debtors)
        {
            var mandateCache = api.GetCache(typeof(Uniconta.DataModel.DebtorPaymentMandate)) ?? await api.LoadCache(typeof(Uniconta.DataModel.DebtorPaymentMandate));

            CWDirectDebit cwwin = new CWDirectDebit(api, string.Format(Uniconta.ClientTools.Localization.lookup("CreateOBJ"), Uniconta.ClientTools.Localization.lookup("Mandates")), true);
            cwwin.Closing += delegate
            {
                if (cwwin.DialogResult == true)
                {
                    var lstDebtors = debtors.Cast<Uniconta.ClientTools.DataModel.DebtorClient>();
                    var lstInsert = new List<Uniconta.DataModel.DebtorPaymentMandate>();
                    var lstUpdate = new List<Uniconta.DataModel.Debtor>();

                    foreach (var rec in lstDebtors)
                    {
                        var mandate = (Uniconta.DataModel.DebtorPaymentMandate)mandateCache?.Get(rec._Account);
                        if (mandate != null)
                            continue;

                        var newMandate = new Uniconta.DataModel.DebtorPaymentMandate();
                        newMandate._DCAccount = rec._Account;
                        newMandate._Scheme = cwwin.directDebitScheme;
                        newMandate._StatusInfo = string.Format("({0}) Mandate created", Uniconta.DirectDebitPayment.Common.GetTimeStamp()); ;
                        lstInsert.Add(newMandate);

                        if (rec.PaymentFormat != cwwin.PaymentFormat._Format)
                        {
                            rec.PaymentFormat = cwwin.PaymentFormat._Format;
                            lstUpdate.Add(rec);
                        }
                    }

                    if (lstInsert.Count > 0)
                    {
                        api.InsertNoResponse(lstInsert);
                        UnicontaMessageBox.Show(string.Format(Uniconta.ClientTools.Localization.lookup("RecordsUpdated"), lstInsert.Count, Uniconta.ClientTools.Localization.lookup("Mandates")), Uniconta.ClientTools.Localization.lookup("Information"));
                    }

                    if (lstUpdate.Count > 0)
                        api.UpdateNoResponse(lstUpdate);
                }
            };
            cwwin.Show();
        }
#endif

        bool copyRowIsEnabled = false;
        bool editAllChecked;
        private void EditAll()
        {
            RibbonBase rb = (RibbonBase)localMenu.DataContext;
            var ibase = UtilDisplay.GetMenuCommandByName(rb, "EditAll");
            if (ibase == null)
                return;
            if (dgDebtorAccountGrid.Readonly)
            {
                api.AllowBackgroundCrud = false;
                dgDebtorAccountGrid.MakeEditable();
                UserFieldControl.MakeEditable(dgDebtorAccountGrid);
                ibase.Caption = Uniconta.ClientTools.Localization.lookup("LeaveEditAll");
                ribbonControl.EnableButtons(new string[] { "AddLine", "CopyRow", "DeleteRow", "UndoDelete", "SaveGrid" });
                copyRowIsEnabled = true;
                editAllChecked = false;
            }
            else
            {
                if (IsDataChaged)
                {
                    string message = Uniconta.ClientTools.Localization.lookup("SaveChangesPrompt");
                    CWConfirmationBox confirmationDialog = new CWConfirmationBox(message);
                    confirmationDialog.Closing += async delegate
                    {
                        if (confirmationDialog.DialogResult == null)
                            return;

                        switch (confirmationDialog.ConfirmationResult)
                        {
                            case CWConfirmationBox.ConfirmationResultEnum.Yes:
                                var err = await dgDebtorAccountGrid.SaveData();
                                if (err != 0)
                                {
                                    api.AllowBackgroundCrud = true;
                                    return;
                                }
                                break;
                            case CWConfirmationBox.ConfirmationResultEnum.No:
                                break;
                        }
                        editAllChecked = true;
                        dgDebtorAccountGrid.Readonly = true;
                        dgDebtorAccountGrid.tableView.CloseEditor();
                        ibase.Caption = Uniconta.ClientTools.Localization.lookup("EditAll");
                        ribbonControl.DisableButtons(new string[] { "AddLine", "CopyRow", "DeleteRow", "UndoDelete", "SaveGrid" });
                        copyRowIsEnabled = false;
                    };
                    confirmationDialog.Show();
                }
                else
                {
                    dgDebtorAccountGrid.Readonly = true;
                    dgDebtorAccountGrid.tableView.CloseEditor();
                    ibase.Caption = Uniconta.ClientTools.Localization.lookup("EditAll");
                    ribbonControl.DisableButtons(new string[] { "AddLine", "CopyRow", "DeleteRow", "UndoDelete", "SaveGrid" });
                    copyRowIsEnabled = false;
                }
            }

        }
        public override bool IsDataChaged
        {
            get
            {
                return editAllChecked ? false : dgDebtorAccountGrid.HasUnsavedData;
            }
        }
        private async void Save()
        {
            dgDebtorAccountGrid.BusyIndicator.IsBusy = true;
            var err = await dgDebtorAccountGrid.SaveData();
            if (err != ErrorCodes.Succes)
                api.AllowBackgroundCrud = true;
            dgDebtorAccountGrid.BusyIndicator.IsBusy = false;
        }
        public override void Utility_Refresh(string screenName, object argument = null)
        {
            if (screenName == TabControls.DebtorAccountPage2)
            {
                dgDebtorAccountGrid.UpdateItemSource(argument);
                if (dgDebtorAccountGrid.Visibility == Visibility.Collapsed)
                    detailControl.Refresh(argument, dgDebtorAccountGrid.SelectedItem);
            }
            if (screenName == TabControls.UserNotesPage || screenName == TabControls.UserDocsPage && argument != null)
                dgDebtorAccountGrid.UpdateItemSource(argument);
        }

        private Task BindGrid()
        {
            return dgDebtorAccountGrid.Filter(null);
        }

        private void HasDocImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var debtorAccount = (sender as Image).Tag as DebtorClient;
            if (debtorAccount != null)
                AddDockItem(TabControls.UserDocsPage, dgDebtorAccountGrid.syncEntity);
        }

        private void HasNoteImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var debtorAccount = (sender as Image).Tag as DebtorClient;
            if (debtorAccount != null)
                AddDockItem(TabControls.UserNotesPage, dgDebtorAccountGrid.syncEntity);
        }
#if !SILVERLIGHT
        private void HasEmailImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var debtorAccount = (sender as TextBlock).Tag as DebtorClient;
            if (debtorAccount != null)
            {
                var mail = string.Concat("mailto:", debtorAccount._ContactEmail);
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = mail;
                proc.Start();
            }
        }

        private void HasWebsite_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var debtorAccount = (sender as TextBlock).Tag as DebtorClient;
            if (debtorAccount != null)
                Utility.OpenWebSite(debtorAccount.Www);
        }
#endif
    }
}
