using UnicontaClient.Models;
using UnicontaClient.Pages;
using UnicontaClient.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Uniconta.ClientTools.DataModel;
using Uniconta.ClientTools.Page;
using Uniconta.Common;
using Uniconta.DataModel;
using System.ComponentModel;
using Uniconta.Common.Utility;
using Uniconta.ClientTools.Util;
using Uniconta.API.Service;
using Uniconta.ClientTools.Controls;

using UnicontaClient.Pages;
namespace UnicontaClient.Pages.CustomPage
{

    /// <summary>
    /// Interaction logic for InvVaraintDetailPage.xaml
    /// </summary>
    public partial class InvVariantDetailPage : GridBasePage
    {
        public override string NameOfControl { get { return TabControls.InvVariantDetailPage; } }
        InvItemClient invMaster;
        SQLCache items;
        public InvVariantDetailPage(UnicontaBaseEntity master)
            : base(master)
        {
            InitializeComponent();
            invMaster = (InvItemClient)master;
            Init();
            Item.Visible = false;
        }

        public InvVariantDetailPage(BaseAPI API) : base(API, string.Empty)
        {
            InitializeComponent();
            Init();
        }

        void Init()
        {
            localMenu.dataGrid = dgInvVariantDetailGrid;
            SetRibbonControl(localMenu, dgInvVariantDetailGrid);
            dgInvVariantDetailGrid.api = api;
            localMenu.OnItemClicked += localMenu_OnItemClicked;
            if (invMaster != null)
                dgInvVariantDetailGrid.UpdateMaster(invMaster);
            dgInvVariantDetailGrid.BusyIndicator = busyIndicator;
            dgInvVariantDetailGrid.View.DataControl.CurrentItemChanged += DataControl_CurrentItemChanged;
            Utility.SetupVariants(api, colVariant, colVariant1, colVariant2, colVariant3, colVariant4, colVariant5, Variant1Name, Variant2Name, Variant3Name, Variant4Name, Variant5Name);
        }

        void DataControl_CurrentItemChanged(object sender, DevExpress.Xpf.Grid.CurrentItemChangedEventArgs e)
        {
            var oldselectedItem = e.OldItem as InvVariantDetailClient;
            if (oldselectedItem != null)
                oldselectedItem.PropertyChanged -= InvVariantDetailClientGrid_PropertyChanged;
            var selectedItem = e.NewItem as InvVariantDetailClient;
            if (selectedItem != null)
                selectedItem.PropertyChanged += InvVariantDetailClientGrid_PropertyChanged;
        }

        private void InvVariantDetailClientGrid_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var rec = (InvVariantDetailClient)sender;
            switch (e.PropertyName)
            {
                case "EAN":
                    if (!Utility.IsValidEAN(rec.EAN, api.CompanyEntity))
                    {
                        UnicontaMessageBox.Show(string.Format("{0} {1}", Uniconta.ClientTools.Localization.lookup("EANinvalid"), rec.EAN), Uniconta.ClientTools.Localization.lookup("Error"));
                        rec.EAN = null;
                    }
                    break;
            }
        }

        private void localMenu_OnItemClicked(string ActionType)
        {
            switch (ActionType)
            {
                case "AddRow":
                    var row = dgInvVariantDetailGrid.AddRow();
                    if (invMaster != null)
                    {
                        var currentRow = row as InvVariantDetailClient;
                        currentRow.Item = invMaster.Item;
                    }
                    break;
                case "CopyRow":
                    dgInvVariantDetailGrid.CopyRow();
                    break;
                case "SaveGrid":
                    saveGrid();
                    break;
                case "DeleteRow":
                    dgInvVariantDetailGrid.DeleteRow();
                    break;
                default:
                    gridRibbon_BaseActions(ActionType);
                    break;
            }
        }

        protected override async Task<ErrorCodes> saveGrid()
        {
            dgInvVariantDetailGrid.SelectedItem = null;
            ErrorCodes res = await dgInvVariantDetailGrid.SaveData();
            if (res == ErrorCodes.Succes)
                globalEvents.OnRefresh(NameOfControl, invMaster);
            return res;
        }

        CorasauGridLookupEditorClient prevVariant1;
        private void variant1_GotFocus(object sender, RoutedEventArgs e)
        {
            if (prevVariant1 != null)
                prevVariant1.isValidate = false;
            var editor = (CorasauGridLookupEditorClient)sender;
            prevVariant1 = editor;
            editor.isValidate = true;
        }

        CorasauGridLookupEditorClient prevVariant2;
        private void variant2_GotFocus(object sender, RoutedEventArgs e)
        {
            if (prevVariant2 != null)
                prevVariant2.isValidate = false;
            var editor = (CorasauGridLookupEditorClient)sender;
            prevVariant2 = editor;
            editor.isValidate = true;
        }

        protected override async void LoadCacheInBackGround()
        {
            var api = this.api;
            var Comp = api.CompanyEntity;
            if (this.items == null)
                this.items = await Comp.LoadCache(typeof(Uniconta.DataModel.InvItem), api).ConfigureAwait(false);
        }

        public override bool CheckIfBindWithUserfield(out bool isReadOnly, out bool useBinding)
        {
            isReadOnly = false;
            useBinding = true;
            return true;
        }
    }
    public class InvVariantDetailGrid : CorasauDataGridClient
    {
        public override Type TableType { get { return typeof(InvVariantDetailClient); } }
        public override string LineNumberProperty { get { return "_LineNumber"; } }
        public override bool AllowSort
        {
            get
            {
                return false;
            }
        }
        public override bool Readonly { get { return false; } }

    }
}
