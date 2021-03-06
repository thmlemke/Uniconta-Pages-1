using Uniconta.API.System;
using UnicontaClient.Models;
using UnicontaClient.Utilities;
using Uniconta.ClientTools;
using Uniconta.ClientTools.DataModel;
using Uniconta.ClientTools.Page;
using Uniconta.Common;
using Uniconta.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows;
using Uniconta.ClientTools.Controls;
using Uniconta.ClientTools.Util;
using DevExpress.Xpf.Editors;

using UnicontaClient.Pages;
namespace UnicontaClient.Pages.CustomPage
{
    public partial class DebtorOfferPage2 : FormBasePage
    {
        DebtorOfferClient editrow;
        SQLCache ProjectCache;
        DebtorClient Debtor;
        ContactClient Contact;
        CrmProspectClient Prospect;
        public override void OnClosePage(object[] RefreshParams)
        {
            globalEvents.OnRefresh(NameOfControl, RefreshParams);
        }

        public override Type TableType { get { return typeof(DebtorOfferClient); } }
        public override UnicontaBaseEntity ModifiedRow { get { return editrow; } set { editrow = (DebtorOfferClient)value; } }

        public DebtorOfferPage2(UnicontaBaseEntity sourcedata, UnicontaBaseEntity master) /* called for edit from particular account */
            : base(sourcedata, true)
        {
            InitializeComponent();
            if (master != null)
            {
                Debtor = master as DebtorClient;
                if (Debtor == null)
                {
                    Prospect = master as CrmProspectClient;
                    if (Prospect == null)
                        Contact = master as ContactClient;
                }
            }
            InitPage(api, master);
        }

        public DebtorOfferPage2(CrudAPI crudApi, UnicontaBaseEntity master) /* called for add from particular account */
            : base(crudApi, "")
        {
            InitializeComponent();
            if (master != null)
            {
                Debtor = master as DebtorClient;
                if (Debtor == null)
                {
                    Prospect = master as CrmProspectClient;
                    if (Prospect == null)
                        Contact = master as ContactClient;
                }
            }
            InitPage(crudApi, master);
        }

        public DebtorOfferPage2(UnicontaBaseEntity sourcedata)
            : base(sourcedata, true)
        {
            InitializeComponent();
            InitPage(api, sourcedata);
        }
        public DebtorOfferPage2(CrudAPI crudApi, string dummy)
            : base(crudApi, dummy)
        {
            InitializeComponent();
            InitPage(crudApi, null);
#if !SILVERLIGHT
            FocusManager.SetFocusedElement(leAccount, leAccount);
#endif
        }

        UnicontaBaseEntity master;

        void InitPage(CrudAPI crudapi, UnicontaBaseEntity master)
        {
            RemoveMenuItem();
            BusyIndicator = busyIndicator;
            dAddress.Header = Uniconta.ClientTools.Localization.lookup("DeliveryAddr");
            layoutControl = layoutItems;
            Employeelookupeditor.api = leAccount.api = lePayment.api = cmbDim1.api = cmbDim2.api = cmbDim3.api = cmbDim4.api = cmbDim5.api = leLayoutGroup.api = leInvoiceAccount.api = PriceListlookupeditior.api =
                leGroup.api = leShipment.api = leDeliveryTerm.api = Projectlookupeditor.api = PrCategorylookupeditor.api = leDeliveryAddress.api = crudapi;
            cbDeliveryCountry.ItemsSource = Enum.GetValues(typeof(Uniconta.Common.CountryCode));
            AdjustLayout();
            if (editrow == null)
            {
                frmRibbon.DisableButtons("Delete");
                editrow = CreateNew() as DebtorOfferClient;
                editrow._Created = DateTime.MinValue;
                liCreatedTime.Visibility = Visibility.Collapsed;
                if (Debtor != null)
                {
                    editrow.SetMaster(Debtor);
                    if (editrow.RowId == 0)
                        SetValuesFromMaster(Debtor);
                    leAccount.IsEnabled = txtName.IsEnabled = false;
                }
                if (Contact != null)
                {
                    editrow.SetMaster(Contact);
                    editrow.ContactName = Contact.Name;
                    cmbContactName.IsEnabled = false;
                }
                editrow.SetMaster(master); // cound be prospect or project
            }
            else
            {
                BindContact(editrow.Debtor);
            }

            if (Prospect != null)
                BindContact(Prospect);
            if (Contact != null)
                BindContact(Contact.Prospect);

            layoutItems.DataContext = editrow;
            frmRibbon.OnItemClicked += frmRibbon_OnItemClicked;
            
            AcItem.ButtonClicked += AcItem_ButtonClicked;

#if !SILVERLIGHT
            editrow.PropertyChanged += Editrow_PropertyChanged;
#endif
            this.master = master;
            if (Prospect != null || Contact != null)
            {
                leAccount.IsEnabled = false;
                AcItem.IsEnabled = false;
                txtName.IsEnabled = false;
                txtName.Text = editrow.Name;
            }

            if (master == null)
            {
                if (crudapi.CompanyEntity.CRM)
                    LoadType(new Type[] { typeof(Uniconta.DataModel.Debtor), typeof(Uniconta.DataModel.CrmProspect) });
                else
                    LoadType(typeof(Uniconta.DataModel.Debtor));
            }
            StartLoadCache();
        }

        void RemoveMenuItem()
        {
            RibbonBase rb = (RibbonBase)frmRibbon.DataContext;
            var Comp = api.CompanyEntity;
            UtilDisplay.RemoveMenuCommand(rb, "PhysicalVou");
        }

        protected override void OnLayoutCtrlLoaded()
        {
              AdjustLayout();
        }

        void AdjustLayout()
        {
            var Comp = api.CompanyEntity;
            if (Comp.NumberOfDimensions == 0)
                usedim.Visibility = Visibility.Collapsed;
            else
                Utility.SetDimensions(api, lbldim1, lbldim2, lbldim3, lbldim4, lbldim5, cmbDim1, cmbDim2, cmbDim3, cmbDim4, cmbDim5, usedim);
            if (!Comp.DeliveryAddress)
                dAddress.Visibility = Visibility.Collapsed;
            if (!Comp.Project)
            {
                projectItem.Visibility = Visibility.Collapsed;
                prCategoryItem.Visibility = Visibility.Collapsed;
            }
        }
        public override bool BeforeSetUserField(ref CorasauLayoutGroup parentGroup)
        {
            parentGroup = lastGroup;
            return true;
        }

        string zip;
        private async void Editrow_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "DeliveryZipCode")
            {
                if (zip == null)
                {
                    var deliveryCountry = editrow.DeliveryCountry ?? editrow.Country;
                    var city = await UtilDisplay.GetCityAndAddress(txtDelZipCode.Text, deliveryCountry);
                    if (city != null)
                    {
                        editrow.DeliveryCity = city[0];
                        var add1 = city[1];
                        if (!string.IsNullOrEmpty(add1))
                            editrow.DeliveryAddress1 = add1;
                        zip = city[2];
                        if (!string.IsNullOrEmpty(zip))
                            editrow.DeliveryZipCode = zip;
                    }
                }
                else
                    zip = null;
            }
        }

        public override void RowsPastedDone()
        {
            editrow.SetMaster(master);
            SetValuesFromMaster(Debtor);
        }

        void AcItem_ButtonClicked(object sender)
        {
            AddDebtor_Click(null, null);
        }

        public override void Utility_Refresh(string screenName, object argument = null)
        {
            if (screenName == TabControls.DebtorAccountPage2)
            {
                var args = argument as object[];
                if (args[2] == this.ParentControl)
                {
                    leAccount.LoadItemSource();
                    var dc = args[3] as UnicontaBaseEntity;
                    editrow.SetMaster(dc);
                }
            }
        }

        private void frmRibbon_OnItemClicked(string ActionType)
        {
            switch (ActionType)
            {
                case "SaveAndOrderLines":
                    saveFormAndOpenControl(TabControls.DebtorOfferLines);
                    break;
                default:
                    frmRibbon_BaseActions(ActionType);
                    break;
            }
        }

        public override async void saveFormAndOpenControl(string Control, string header = null)
        {
            closePageOnSave = false;
            var res = await saveForm(false);
            closePageOnSave = true;
            if (res)
            {
                header = string.Format("{0}:{1},{2}", Uniconta.ClientTools.Localization.lookup("OfferLine"), editrow._OrderNumber, editrow.Name);
                AddDockItem(Control, ModifiedRow, header);
                dockCtrl?.JustClosePanel(this.ParentControl);
            }
        }
        private void AddDebtor_Click(object sender, RoutedEventArgs e)
        {
            object[] param = new object[2];
            param[0] = api;
            param[1] = null;
            AddDockItem(TabControls.DebtorAccountPage2, param, Uniconta.ClientTools.Localization.lookup("DebtorAccount"), ";component/Assets/img/Add_16x16.png");
        }

        private void leAccount_EditValueChanged(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        {
            string id = Convert.ToString(e.NewValue);
            var debtors = api.CompanyEntity.GetCache(typeof(Debtor));
            if (debtors != null && Contact == null)
                SetValuesFromMaster((Debtor)debtors.Get(id));
        }

        async void SetValuesFromMaster(Debtor debtor)
        {
            if (debtor == null)
                return;
            var loadedOrder = LoadedRow as DCOrder;
            if (loadedOrder?._DCAccount == debtor._Account)
                return;
            editrow.Account = debtor._Account;
            editrow.SetCurrency(debtor._Currency);
            editrow.Payment = debtor._Payment;
            editrow.PricesInclVat = debtor._PricesInclVat;
            editrow.EndDiscountPct = debtor._EndDiscountPct;
            editrow.PostingAccount = debtor._PostingAccount;
            editrow.LayoutGroup = debtor._LayoutGroup;
            editrow.Shipment = debtor._Shipment;
            editrow.Employee = debtor._Employee;
            editrow.DeliveryTerm = debtor._DeliveryTerm;
            editrow.PriceList = debtor._PriceList;
            if (!RecordLoadedFromTemplate || debtor._DeliveryAddress1 != null)
            {
                editrow.DeliveryName = debtor._DeliveryName;
                editrow.DeliveryAddress1 = debtor._DeliveryAddress1;
                editrow.DeliveryAddress2 = debtor._DeliveryAddress2;
                editrow.DeliveryAddress3 = debtor._DeliveryAddress3;
                editrow.DeliveryZipCode = debtor._DeliveryZipCode;
                editrow.DeliveryCity = debtor._DeliveryCity;
                if (debtor._DeliveryCountry != 0)
                    editrow.DeliveryCountry = debtor._DeliveryCountry;
                else
                    editrow.DeliveryCountry = null;
            }
            TableField.SetUserFieldsFromRecord(debtor, editrow);
            if (ProjectCache != null)
            {
                Projectlookupeditor.cacheFilter = new DebtorProjectFilter(ProjectCache, debtor._Account);
                Projectlookupeditor.InvalidCache();
            }

            BindContact(debtor);
            if (installationCache != null)
            {
                leDeliveryAddress.cacheFilter = new AccountCacheFilter(installationCache, 1, debtor._Account);
                leDeliveryAddress.InvalidCache();
            }
            await api.Read(debtor);
            editrow.RefreshBalance();
        }

        int contactRefId;
        void SetContactSource(SQLCache cache, Debtor debtor)
        {
            cmbContactName.ItemsSource = ((IEnumerable<Uniconta.DataModel.Contact>)cache?.GetNotNullArray).Where(x => x._DCType == 1 && x._DCAccount == debtor._Account);
        }

        async void BindContact(Debtor debtor)
        {
            if (debtor == null) return;

            var cache = api.GetCache(typeof(Uniconta.DataModel.Contact)) ?? await api.LoadCache(typeof(Uniconta.DataModel.Contact));
            SetContactSource(cache, debtor);
            cmbContactName.DisplayMember = "KeyName";

            contactRefId = editrow._ContactRef;
            if (contactRefId != 0 && cache != null)
            {
                var contact = cache.Get(contactRefId);
                if (contact == null)
                {
                    cache = api.LoadCache(typeof(Uniconta.DataModel.Contact), true).GetAwaiter().GetResult();
                    contact = cache.Get(contactRefId);
                    SetContactSource(cache, debtor);
                }
                cmbContactName.SelectedItem = contact;
                if (contact == null)
                {
                    editrow._ContactRef = 0;
                    editrow.ContactName = null;
                }
            }
        }

        async void BindContact(CrmProspect prospect)
        {
            if (prospect == null)
            {
                var list = new List<ContactClient>();
                list.Add(Contact);
                cmbContactName.ItemsSource = list;
                cmbContactName.DisplayMember = "KeyName";
                cmbContactName.SelectedItem = list.FirstOrDefault();
            }
            else
            {
                contactRefId = editrow._ContactRef;
                var cache = api.GetCache(typeof(Uniconta.DataModel.Contact)) ?? await api.LoadCache(typeof(Uniconta.DataModel.Contact));
                var items = (cache != null) ? ((IEnumerable<Uniconta.DataModel.Contact>)cache.GetNotNullArray).Where(x => x._DCType == 3 && x._DCAccount == prospect.KeyStr) : null;
                cmbContactName.ItemsSource = items;
                cmbContactName.DisplayMember = "KeyName";

                if (contactRefId != 0 && items != null)
                {
                    var contact = items.Where(x => x.RowId == contactRefId).FirstOrDefault();
                    cmbContactName.SelectedItem = contact;
                    if (contact == null)
                    {
                        editrow._ContactRef = 0;
                        editrow.ContactName = null;
                    }
                }
            }
        }

        private void cmbContactName_EditValueChanged(object sender, EditValueChangedEventArgs e)
        {
            var selectedItem = cmbContactName.SelectedItem as Contact;
            if (selectedItem != null)
            {
                editrow._ContactRef = selectedItem.RowId;
                editrow.ContactName = selectedItem._Name;
            }
            else
            {
                editrow._ContactRef = 0;
                editrow.ContactName = null;
            }
        }

#if !SILVERLIGHT
        private void LiDeliveryZipCode_OnButtonClicked(object sender)
        {
            var location = editrow.DeliveryAddress1 + "+" + editrow.DeliveryAddress2 + "+" + editrow.DeliveryAddress3 + "+" + editrow.DeliveryZipCode + "+" + editrow.DeliveryCity + "+" + editrow.DeliveryCountry;
            Utility.OpenGoogleMap(location);
        }
#endif
        SQLCache installationCache;
        protected override async void LoadCacheInBackGround()
        {
            var api = this.api;
            var Comp = api.CompanyEntity;

            if (Comp.Project)
            {
                ProjectCache = api.GetCache(typeof(Uniconta.DataModel.Project)) ?? await api.LoadCache(typeof(Uniconta.DataModel.Project)).ConfigureAwait(false);
                if (editrow._DCAccount != null)
                    Projectlookupeditor.cacheFilter = new DebtorProjectFilter(ProjectCache, editrow._DCAccount);

                var prCache = api.GetCache(typeof(Uniconta.DataModel.PrCategory)) ?? await api.LoadCache(typeof(Uniconta.DataModel.PrCategory)).ConfigureAwait(false);
                PrCategorylookupeditor.cacheFilter = new PrCategoryRevenueFilter(prCache);
            }

            if (Comp.DeliveryAddress)
            {
                installationCache = api.GetCache(typeof(Uniconta.DataModel.WorkInstallation)) ?? await api.LoadCache(typeof(Uniconta.DataModel.WorkInstallation)).ConfigureAwait(false);
                if (editrow._DCAccount != null)
                    leDeliveryAddress.cacheFilter = new AccountCacheFilter(installationCache, 1, editrow._DCAccount);
            }
        }
    }
}
