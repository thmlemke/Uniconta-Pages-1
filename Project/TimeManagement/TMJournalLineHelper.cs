using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Uniconta.API.System;
using Uniconta.ClientTools.Controls;
using Uniconta.ClientTools.DataModel;
using Uniconta.Common;
using Uniconta.DataModel;
using UnicontaClient.Pages;

using UnicontaClient.Pages;
namespace UnicontaClient.Pages.CustomPage.Project.TimeManagement
{
    public class TMJournalLineError
    {
        public string Message;
        public int RowId;
    }

    public class TMJournalLineHelper
    {
        #region Constants
        public const string VALIDATE_OK = "Ok";
        #endregion

        #region Variables
        private Company comp;
        private bool err;
        private List<TMJournalLineError> checkErrors;
        private EmpPayrollCategoryEmployeeClient[] empPriceLst;
        private DateTime startDate;
        private DateTime endDate;
        private CrudAPI api;
        private Uniconta.DataModel.Employee employee;
        private SQLTableCache<Uniconta.DataModel.EmpPayrollCategory> empPayrollCatList;
        private SQLTableCache<Uniconta.DataModel.ProjectGroup> projGroupList;
        private SQLTableCache<Uniconta.ClientTools.DataModel.ProjectClient> projCache;


        #endregion

        public enum TMJournalActionType { Close, Open, Approve, Validate }

        public TMJournalLineHelper(CrudAPI api, Uniconta.DataModel.Employee employee)
        {
            this.api = api;
            this.employee = employee;
        }

        public TMJournalLineHelper(CrudAPI api)
        {
            this.api = api;
        }

        #region Prevalidate
        public List<TMJournalLineError> PreValidate(TMJournalActionType actionType, double valNormHours, double valRegHours, SQLTableCache<Uniconta.DataModel.EmpPayrollCategory> empPayrollCatList)
        {
            checkErrors = new List<TMJournalLineError>();

            this.empPayrollCatList = empPayrollCatList;

            PreValidateEmployee();
            PreValidateCalendar(valNormHours, valRegHours, actionType);
            PreValidatePayrollCategory();

            return checkErrors;
        }


        /// <summary>
        /// Validate - Employee
        /// </summary>
        private void PreValidateEmployee()
        {
            if (employee._TMCloseDate < employee._TMApproveDate && employee._TMCloseDate != DateTime.MinValue && employee._TMApproveDate != DateTime.MinValue)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = string.Format("Close Date ('{0}') is less than Approve Date ('{1}'). This is not allowed. Please change the dates for the Employee.", employee._TMCloseDate.ToShortDateString(), employee._TMApproveDate.ToShortDateString()),
                });
            }
        }

        /// <summary>
        /// Validate - Norm Hours
        /// </summary>
        private void PreValidateCalendar(double valNormHours, double valRegHours, TMJournalActionType actionType)
        {
            if ((actionType == TMJournalActionType.Validate || actionType == TMJournalActionType.Close) && (valRegHours - valNormHours) < 0)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = Uniconta.ClientTools.Localization.lookup("NormHoursNotFulfilled"),
                });
            }
        }

        // <summary>
        // Validate - Payroll category
        // </summary>
        private void PreValidatePayrollCategory()
        {
            if (empPayrollCatList == null)
                return;

            foreach (var rec in empPayrollCatList.Where(s => s._InternalType != InternalType.None))
            {
                if (rec._InternalProject == null)
                {
                    checkErrors.Add(new TMJournalLineError()
                    {
                        Message = String.Format("Payroll category '{0}' must have specified an Internal Project", rec.KeyStr),
                        RowId = rec.RowId
                    });

                    err = true;
                }
            }
        }

        #endregion

        #region Validate Hours
        public List<TMJournalLineError> ValidateLinesHours(IEnumerable<TMJournalLineClientLocal> lines, 
                                                           DateTime startDate, 
                                                           DateTime endDate, 
                                                           EmpPayrollCategoryEmployeeClient[] empPriceLst,
                                                           SQLTableCache<Uniconta.DataModel.EmpPayrollCategory> empPayrollCatList,
                                                           SQLTableCache<Uniconta.ClientTools.DataModel.ProjectClient> projCache,
                                                           SQLTableCache<Uniconta.DataModel.ProjectGroup> projGroupList,
                                                           Uniconta.DataModel.Employee employee)
        {
            checkErrors = new List<TMJournalLineError>();

            this.empPayrollCatList = empPayrollCatList;
            this.projGroupList = projGroupList;
            this.projCache = projCache;
            this.comp = api.CompanyEntity;
            this.empPriceLst = empPriceLst;
            this.employee = employee;
            var approveDate = employee._TMApproveDate;
            this.startDate = approveDate >= startDate ? approveDate.AddDays(1) : startDate;
            this.endDate = endDate;

            foreach (var rec in lines)
            {
                rec.ErrorInfo = string.Empty;
                err = false;

                ValidateHoursGeneral(rec);
                ValidateHoursProject(rec);
                ValidateHoursProjectCategory(rec);

                if (!err)
                    rec.ErrorInfo = VALIDATE_OK;
            }

            if (!err)
                ValidateHoursPrice(lines);

            return checkErrors;
        }


        /// <summary>
        /// Validate - General
        /// </summary>
        private void ValidateHoursGeneral(TMJournalLineClientLocal rec)
        {
            if (err)
                return;

            if (rec.Project == null)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = fieldCannotBeEmpty("ProjectNumber"),
                    RowId = rec.RowId
                });

                err = true;
            }

            if (rec.PayrollCategory == null)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = fieldCannotBeEmpty("PayrollCategory"),
                    RowId = rec.RowId
                });

                err = true;
            }
        }

        /// <summary>
        /// Validate - Prices
        /// </summary>
        private void ValidateHoursPrice(IEnumerable<TMJournalLineClientLocal> lines) //TODO:TEST
        {
            SetEmplPrice(lines, 
                         empPriceLst,
                         this.empPayrollCatList,
                         this.projCache,
                         startDate, 
                         endDate, 
                         this.employee,
                         true);
        }

        private static string fieldCannotBeEmpty(string field)
        {
            return String.Format("{0} ({1})",
                    Uniconta.ClientTools.Localization.lookup("FieldCannotBeEmpty"),
                    Uniconta.ClientTools.Localization.lookup(field));
        }

        /// <summary>
        /// Validate - Project
        /// </summary>
        private void ValidateHoursProject(TMJournalLineClientLocal rec)
        {
            if (err)
                return;

            if (rec.ProjectRef.Blocked)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = Uniconta.ClientTools.Localization.lookup("ProjectIsBlocked"),
                    RowId = rec.RowId
                });

                err = true;
            }
        }

        /// <summary>
        /// Validate - Project Category
        /// </summary>
        private void ValidateHoursProjectCategory(TMJournalLineClientLocal rec)
        {
            if (err)
                return;

            var payrollCat = empPayrollCatList?.Get(rec.PayrollCategory);
            var projGroup = projGroupList?.Get(rec.ProjectRef.Group);

            if (payrollCat._PrCategory == null)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = String.Format("Payroll category '{0}' need to be updated with a Project category.", rec.PayrollCategory),
                    RowId = rec.RowId
                });

                err = true;
            }

            if (projGroup != null && payrollCat._Invoiceable && !projGroup._Invoiceable)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = String.Format("Invoiceable Payroll category '{0}' cant be posted to a none Invoiceable project", rec.PayrollCategory),
                    RowId = rec.RowId
                });

                err = true;
            }


            if (projGroup != null && !payrollCat._Invoiceable && projGroup._Invoiceable)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = String.Format("None invoiceable Payroll category '{0}' cant be posted to a Invoiceable project", rec.PayrollCategory),
                    RowId = rec.RowId
                });

                err = true;
            }


            if (payrollCat._InternalType == InternalType.Mileage)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = String.Format("Mileage must be registrered in Mileage section"),
                    RowId = rec.RowId
                });

                err = true;
            }

            if (payrollCat._InternalType != InternalType.None && rec.Project != payrollCat._InternalProject)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = String.Format("Project '{0}' is not allowed for Payroll category '{1}' due to setup", rec.Project, rec.PayrollCategory),
                    RowId = rec.RowId
                });
                err = true;
            }
        }
        #endregion
        
        #region Validate Mileage
        public List<TMJournalLineError> ValidateLinesMileage(IEnumerable<TMJournalLineClientLocal> lines, DateTime startDate, SQLTableCache<Uniconta.DataModel.EmpPayrollCategory> empPayrollCatList)
        {
            checkErrors = new List<TMJournalLineError>();

            this.empPayrollCatList = empPayrollCatList;
            this.comp = api.CompanyEntity;
            this.startDate = startDate;

            foreach (var rec in lines)
            {
                rec.ErrorInfo = string.Empty;
                err = false;

                ValidateMileageGeneral(rec);
                ValidateMileageProject(rec);
                ValidateMileageProjectCategory(rec);

                if (!err)
                    rec.ErrorInfo = VALIDATE_OK;
            }

            return checkErrors;
        }

        /// <summary>
        /// Validate - General
        /// </summary>
        private void ValidateMileageGeneral(TMJournalLineClientLocal rec)
        {
            if (err)
                return;

            if (rec.Project == null)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = fieldCannotBeEmpty("ProjectNumber"),
                    RowId = rec.RowId
                });

                err = true;
            }
            
            if (rec.PayrollCategory == null)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = fieldCannotBeEmpty("PayrollCategory"),
                    RowId = rec.RowId
                });

                err = true;
            }

            if (rec.AddressFrom == null)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = fieldCannotBeEmpty("AddressFrom"),
                    RowId = rec.RowId
                });

                err = true;
            }

            if (rec.AddressTo == null)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = fieldCannotBeEmpty("AddressTo"),
                    RowId = rec.RowId
                });

                err = true;
            }

            if (rec.Text == null)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = fieldCannotBeEmpty("Purpose"),
                    RowId = rec.RowId
                });

                err = true;
            }

            //TODO:Disabled until VechicleRegNo can be registered in table.Employee
            //if (rec.VechicleRegNo == null)
            //{
            //    checkErrors.Add(new TMJournalLineError()
            //    {
            //        Message = fieldCannotBeEmpty("VechicleRegNo"),
            //        RowId = rec.RowId
            //    });

            //    err = true;
            //}

        }

        /// <summary>
        /// Validate - Project
        /// </summary>
        private void ValidateMileageProject(TMJournalLineClientLocal rec)
        {
            if (err)
                return;

            if (rec.ProjectRef.Blocked)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = Uniconta.ClientTools.Localization.lookup("ProjectIsBlocked"),
                    RowId = rec.RowId
                });

                err = true;
            }
        }

        /// <summary>
        /// Validate - Project Category
        /// </summary>
        private void ValidateMileageProjectCategory(TMJournalLineClientLocal rec)
        {
            if (err)
                return;

            var payrollCat = empPayrollCatList?.Get(rec.PayrollCategory);

            if (payrollCat._PrCategory == null)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = String.Format("Payroll category '{0}' need to be updated with a Project category.", rec.PayrollCategory),
                    RowId = rec.RowId
                });

                err = true;
            }

            if (payrollCat._Rate == 0)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = String.Format("Payroll category '{0}' need to be specified with kilometer rate.", rec.PayrollCategory),
                    RowId = rec.RowId
                });

                err = true;
            }

            if (payrollCat._InternalType != InternalType.Mileage)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = String.Format("Payroll category '{0}' must have Internaltype '{1}'", rec.PayrollCategory, InternalType.Mileage),
                    RowId = rec.RowId
                });

                err = true;
            }

            if (payrollCat._InternalProject == null)
            {
                checkErrors.Add(new TMJournalLineError()
                {
                    Message = String.Format("Payroll category '{0}' must have an Internal Project", rec.PayrollCategory),
                    RowId = rec.RowId
                });

                err = true;
            }
        }

        #endregion

        public void SetEmplPrice(IEnumerable<TMJournalLineClientLocal> lst, 
                                 EmpPayrollCategoryEmployeeClient[] empPriceLst, 
                                 SQLTableCache<Uniconta.DataModel.EmpPayrollCategory> empPayrollCatLst,
                                 SQLTableCache<Uniconta.ClientTools.DataModel.ProjectClient> projLst,
                                 DateTime startDate, 
                                 DateTime endDate,
                                 Uniconta.DataModel.Employee employee,
                                 bool validate = false)
        {
#if !SILVERLIGHT
            bool foundErr;
            try
            {
                this.employee = employee;
                int dayOfWeekStart = startDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)startDate.DayOfWeek;
                int dayOfWeekEnd = endDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)endDate.DayOfWeek;

                var defaultPayrollCategory = empPayrollCatLst.Where(s => s.KeyStr == "Default" && s._PrCategory == null).FirstOrDefault(); 

                foreach (var trans in lst)
                {
                    var payrollCat = empPayrollCatLst?.Get(trans.PayrollCategory);

                    foundErr = false;
                    for (int x = dayOfWeekStart; x <= dayOfWeekEnd; x++)
                    {
                        if ((double)trans.GetType().GetProperty(string.Format("Day{0}", x)).GetValue(trans) == 0)
                            continue;

                        double salesPrice = 0, costPrice = 0;

                        List<EmpPayrollCategoryEmployeeClient> prices = null;
                        if (empPriceLst != null && empPriceLst.Any()) 
                        {
                            prices = empPriceLst.Where(s => s.Employee == trans.Employee &&
                                                                s.PayrollCategory == trans.PayrollCategory &&
                                                                s.Account == null &&
                                                                s.Project == trans.Project &&
                                                                (s.ValidTo >= startDate.AddDays(x - 1) || s.ValidTo == DateTime.MinValue) && s.ValidFrom <= startDate.AddDays(x - 1)).ToList();

                            if (prices.Count == 0)
                                prices = empPriceLst.Where(s => s.Employee == trans.Employee &&
                                                                s.PayrollCategory == trans.PayrollCategory &&
                                                                s.Account == projLst?.Get(trans.Project)._DCAccount &&
                                                                s.Project == null &&
                                                               (s.ValidTo >= startDate.AddDays(x - 1) || s.ValidTo == DateTime.MinValue) && s.ValidFrom <= startDate.AddDays(x - 1)).ToList();

                            if (prices.Count == 0)
                                prices = empPriceLst.Where(s => s.Employee == trans.Employee &&
                                                                (s.PayrollCategory == trans.PayrollCategory || (defaultPayrollCategory != null && s.PayrollCategory == defaultPayrollCategory.KeyStr)) &&
                                                                s.Account == null &&
                                                                s.Project == null &&
                                                                (s.ValidTo >= startDate.AddDays(x - 1) || s.ValidTo == DateTime.MinValue) && s.ValidFrom <= startDate.AddDays(x - 1)).ToList();
                        }

   
                        if (prices != null && prices.Count() != 0)
                        {
                            salesPrice = trans.Invoiceable ? prices.OrderByDescending(s => s.PrCategory).FirstOrDefault().SalesPrice : 0;
                            costPrice = prices.OrderByDescending(s => s.PrCategory).FirstOrDefault().CostPrice;

                            trans.GetType().GetProperty(string.Format("SalesPriceDay{0}", x)).SetValue(trans, salesPrice);
                            trans.GetType().GetProperty(string.Format("CostPriceDay{0}", x)).SetValue(trans, costPrice);
                        }
                        else
                        {
                            if (trans.Invoiceable)
                                salesPrice = payrollCat._SalesPrice != 0 ? payrollCat._SalesPrice : this.employee._SalesPrice;
                            else
                                salesPrice = 0;

                            costPrice = this.employee._CostPrice;

                            trans.GetType().GetProperty(string.Format("SalesPriceDay{0}", x)).SetValue(trans, salesPrice);
                            trans.GetType().GetProperty(string.Format("CostPriceDay{0}", x)).SetValue(trans, costPrice);
                        }

                        if (validate && foundErr == false)
                        {
                            if (salesPrice == 0 && costPrice == 0)
                            {
                                if (startDate.AddDays(x - 1) >= employee._Hired)
                                {
                                    checkErrors.Add(new TMJournalLineError()
                                    {
                                        Message = Uniconta.ClientTools.Localization.lookup("NoRatesEmployee"),
                                        RowId = trans.RowId
                                    });

                                    trans.ErrorInfo = string.Empty;
                                    err = true;
                                    foundErr = true;
                                }
                            }
                            else
                            {
                                if (prices != null && prices.Where(s => defaultPayrollCategory == null || defaultPayrollCategory != null && s.PayrollCategory != defaultPayrollCategory.KeyStr).Count() > 1)
                                {
                                    var emplPriceRecord = prices.Where(s => defaultPayrollCategory == null || defaultPayrollCategory != null && s.PayrollCategory != defaultPayrollCategory.KeyStr).FirstOrDefault();

                                    checkErrors.Add(new TMJournalLineError()
                                    {
                                        Message = string.Format("{0} ({1}:{2}, {3}:{4}, {5}:{6}, {7}:{8}, {9}:{10})",
                                        Uniconta.ClientTools.Localization.lookup("OverlappingPeriodPriceMatrix"),
                                        Uniconta.ClientTools.Localization.lookup("Date"),
                                        startDate.AddDays(x - 1).ToString("dd.MM.yyyy"),
                                        Uniconta.ClientTools.Localization.lookup("Employee"),
                                        emplPriceRecord.Employee,
                                        Uniconta.ClientTools.Localization.lookup("PayrollCategory"),
                                        emplPriceRecord.PayrollCategory,
                                        Uniconta.ClientTools.Localization.lookup("Account"),
                                        emplPriceRecord.Account,
                                        Uniconta.ClientTools.Localization.lookup("Project"),
                                        emplPriceRecord.Project),
                                        RowId = trans.RowId
                                    });

                                    trans.ErrorInfo = string.Empty;
                                    err = true;
                                    foundErr = true;
                                }

                                if (!foundErr && salesPrice == 0 && trans.Invoiceable)
                                {
                                    checkErrors.Add(new TMJournalLineError()
                                    {
                                        Message = string.Format("{0} ({1}:{2})",
                                        Uniconta.ClientTools.Localization.lookup("NoSalesPrice"),
                                        Uniconta.ClientTools.Localization.lookup("Date"),
                                        startDate.AddDays(x - 1).ToString("dd.MM.yyyy")),
                                        RowId = trans.RowId
                                    });

                                    trans.ErrorInfo = string.Empty;
                                    err = true;
                                    foundErr = true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Uniconta.ClientTools.Controls.UnicontaMessageBox.Show(ex.Message, Uniconta.ClientTools.Localization.lookup("Exception"));
            }
#endif
        }

        public static string GetPeriod(DateTime Date)
        {
            var dayOffset = DayOfWeek.Thursday - Date.DayOfWeek;
            if (dayOffset > 0)
                Date = Date.AddDays(dayOffset);

            var week = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(Date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday).ToString();

            return string.Format("{0}_{1}", Date.Year, week.PadLeft(2, '0'));
        }

        #region TMJournalLineClientPage
        public class TMJournalLineClientLocal : TMJournalLineClient 
        {

            internal object _projectSource;
            public object ProjectSource { get { return _projectSource; } }


            private string _ErrorInfo;
            [Display(Name = "SystemInfo", ResourceType = typeof(DCTransText))]
            public string ErrorInfo { get { return _ErrorInfo; } set { _ErrorInfo = value; NotifyPropertyChanged("ErrorInfo"); } }

            private Double _SalesPriceDay1;
            [Display(Name = "SalesPriceDay1")]
            public Double SalesPriceDay1 { get { return _SalesPriceDay1; } set { _SalesPriceDay1 = value; NotifyPropertyChanged("SalesPriceDay1"); } }

            private Double _SalesPriceDay2;
            [Display(Name = "SalesPriceDay2")]
            public Double SalesPriceDay2 { get { return _SalesPriceDay2; } set { _SalesPriceDay2 = value; NotifyPropertyChanged("SalesPriceDay2"); } }

            private Double _SalesPriceDay3;
            [Display(Name = "SalesPriceDay3")]
            public Double SalesPriceDay3 { get { return _SalesPriceDay3; } set { _SalesPriceDay3 = value; NotifyPropertyChanged("SalesPriceDay3"); } }

            private Double _SalesPriceDay4;
            [Display(Name = "SalesPriceDay4")]
            public Double SalesPriceDay4 { get { return _SalesPriceDay4; } set { _SalesPriceDay4 = value; NotifyPropertyChanged("SalesPriceDay4"); } }

            private Double _SalesPriceDay5;
            [Display(Name = "SalesPriceDay5")]
            public Double SalesPriceDay5 { get { return _SalesPriceDay5; } set { _SalesPriceDay5 = value; NotifyPropertyChanged("SalesPriceDay5"); } }

            private Double _SalesPriceDay6;
            [Display(Name = "SalesPriceDay6")]
            public Double SalesPriceDay6 { get { return _SalesPriceDay6; } set { _SalesPriceDay6 = value; NotifyPropertyChanged("SalesPriceDay6"); } }

            private Double _SalesPriceDay7;
            [Display(Name = "SalesPriceDay7")]
            public Double SalesPriceDay7 { get { return _SalesPriceDay7; } set { _SalesPriceDay7 = value; NotifyPropertyChanged("SalesPriceDay7"); } }

            private Double _CostPriceDay1;
            [Display(Name = "CostPriceDay1")]
            public Double CostPriceDay1 { get { return _CostPriceDay1; } set { _CostPriceDay1 = value; NotifyPropertyChanged("CostPriceDay1"); } }

            private Double _CostPriceDay2;
            [Display(Name = "CostPriceDay2")]
            public Double CostPriceDay2 { get { return _CostPriceDay2; } set { _CostPriceDay2 = value; NotifyPropertyChanged("CostPriceDay2"); } }

            private Double _CostPriceDay3;
            [Display(Name = "CostPriceDay3")]
            public Double CostPriceDay3 { get { return _CostPriceDay3; } set { _CostPriceDay3 = value; NotifyPropertyChanged("CostPriceDay3"); } }

            private Double _CostPriceDay4;
            [Display(Name = "CostPriceDay4")]
            public Double CostPriceDay4 { get { return _CostPriceDay4; } set { _CostPriceDay4 = value; NotifyPropertyChanged("CostPriceDay4"); } }

            private Double _CostPriceDay5;
            [Display(Name = "CostPriceDay5")]
            public Double CostPriceDay5 { get { return _CostPriceDay5; } set { _CostPriceDay5 = value; NotifyPropertyChanged("CostPriceDay5"); } }

            private Double _CostPriceDay6;
            [Display(Name = "CostPriceDay6")]
            public Double CostPriceDay6 { get { return _CostPriceDay6; } set { _CostPriceDay6 = value; NotifyPropertyChanged("CostPriceDay6"); } }

            private Double _CostPriceDay7;
            [Display(Name = "CostPriceDay7")]
            public Double CostPriceDay7 { get { return _CostPriceDay7; } set { _CostPriceDay7 = value; NotifyPropertyChanged("CostPriceDay7"); } }


            public int StatusDay1 { get { return GetStatus(_Date); } }
            public int StatusDay2 { get { return GetStatus(_Date.AddDays(1)); } }
            public int StatusDay3 { get { return GetStatus(_Date.AddDays(2)); } }
            public int StatusDay4 { get { return GetStatus(_Date.AddDays(3)); } }
            public int StatusDay5 { get { return GetStatus(_Date.AddDays(4)); } }
            public int StatusDay6 { get { return GetStatus(_Date.AddDays(5)); } }
            public int StatusDay7 { get { return GetStatus(_Date.AddDays(6)); } }

            public int IsEditable
            {
                get
                {
                    if (_RowId > 0 && !AllSevenDaysStatus(_Date, true))
                        return 1;
                    else
                        return 0;
                }
            }

            int GetStatus(DateTime date)
            {
                int _getStatus = 0;
                if (EmployeeRef._Hired != DateTime.MinValue && date < EmployeeRef._Hired)
                    _getStatus = 3;
                else if (EmployeeRef._TMCloseDate == DateTime.MinValue || date > EmployeeRef._TMCloseDate)
                    _getStatus = 0; //Editable
                else if (date <= EmployeeRef._TMApproveDate)
                    _getStatus = 2; //Non Edittable green
                else if (date <= EmployeeRef._TMCloseDate)
                    _getStatus = 1; //Non Edittable yellow
                return _getStatus;
            }

            bool AllSevenDaysStatus(DateTime Date, bool isStartdate = false)
            {
                bool _status = false;
                if (isStartdate && Date > EmployeeRef._TMCloseDate)
                    _status = true;
                if (isStartdate && Date <= EmployeeRef._TMCloseDate && (Total == 0d || IsFieldEditable()))
                    _status = true;
                return _status;
            }

            DateTime FirstDayOfWeek(DateTime selectedDate)
            {
                var dt = selectedDate;
                int diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
                return dt.AddDays(-1 * diff).Date;
            }

            bool IsFieldEditable()
            {
                bool isFieldEditable = false;
                var lstDictionary = new Dictionary<string, double>();
                if (StatusDay1 == 1 || StatusDay1 == 2)
                    lstDictionary.Add("Day1", Day1);
                if (StatusDay2 == 1 || StatusDay2 == 2)
                    lstDictionary.Add("Day2", Day2);
                if (StatusDay3 == 1 || StatusDay3 == 2)
                    lstDictionary.Add("Day3", Day3);
                if (StatusDay4 == 1 || StatusDay4 == 2)
                    lstDictionary.Add("Day4", Day4);
                if (StatusDay5 == 1 || StatusDay5 == 2)
                    lstDictionary.Add("Day5", Day5);
                if (StatusDay6 == 1 || StatusDay6 == 2)
                    lstDictionary.Add("Day6", Day6);
                if (StatusDay7 == 1 || StatusDay7 == 2)
                    lstDictionary.Add("Day7", Day7);

                var count = lstDictionary.Sum(x => x.Value);
                if (count == 0)
                    isFieldEditable = true;
                return isFieldEditable;
            }

            public bool IsMatched
            {
                get
                {
                    bool isMatched = false;
                    if (_RegistrationType == Uniconta.DataModel.RegistrationType.Hours)
                        if (!string.IsNullOrEmpty(PayrollCategory) && !string.IsNullOrEmpty(InternalType))
                            isMatched = true;
                    return isMatched;
                }
            }
        }
        #endregion

        #region Google Maps calculate distance
        public static double GetDistance(string fromAddress, string toAddress, bool avoidFerries = true, int decimals = 1)
        {
            var distance = 0;

            string apiKey = "AIzaSyC1g0oHHskaL5_o059GBCNzKcvhJEsF-Dg";
            string googlemapURL = "https://maps.googleapis.com/maps/api/distancematrix/json?&origins=";

            fromAddress = new String(fromAddress.Select(ch => ch == ' ' || ch == '\n' ? '+' : ch).ToArray());
            toAddress = new String(toAddress.Select(ch => ch == ' ' || ch == '\n' ? '+' : ch).ToArray());

            if (fromAddress == string.Empty || toAddress == string.Empty)
                return 0;

            string requesturl = string.Format("{0}{1}&destinations={2}&mode=driving{3}&key={4}", googlemapURL, fromAddress, toAddress, avoidFerries ? "&avoid=ferries" : string.Empty, apiKey);
            string content = FileGetContents(requesturl);

            JObject o = JObject.Parse(content);
            try
            {
                distance = (int)o.SelectToken("rows[0].elements[0].distance.value");
                return Math.Round((double)distance / 1000, decimals);
            }
            catch
            {
                return distance;
            }
        }
#if !SILVERLIGHT
        private static string FileGetContents(string fileName)
        {
            string sContents = string.Empty;
            try
            {
                System.Net.WebClient wc = new System.Net.WebClient();
                byte[] response = wc.DownloadData(fileName);
                sContents = System.Text.Encoding.ASCII.GetString(response);
            }
            catch
            {
                UnicontaMessageBox.Show(string.Format("{0} {1}", Uniconta.ClientTools.Localization.lookup("UnableToConnectTo"), "Google Maps"), Uniconta.ClientTools.Localization.lookup("Error"));
            }

            return sContents;
        }
#else
        private static string FileGetContents(string fileName)
        {
            string sContents = string.Empty;
            //try
            //{
            //    System.Net.WebClient wc = new System.Net.WebClient();
            //    wc.DownloadStringCompleted += new DownloadStringCompletedEventHandler(client_DownloadStringCompleted);
            //    wc.DownloadStringAsync(new Uri(fileName, UriKind.RelativeOrAbsolute));
            //    //  sContents = System.Text.Encoding.UTF8.GetString(response);
            //}
            //catch
            //{
            //    UnicontaMessageBox.Show(string.Format("{0} {1}", Uniconta.ClientTools.Localization.lookup("UnableToConnectTo"), "Google Maps"), Uniconta.ClientTools.Localization.lookup("Error"));
            //}

            return sContents;
        }
        private static void client_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                string response = e.Result;
            }
        }
#endif
        #endregion
    }
}
