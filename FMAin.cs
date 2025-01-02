using CareFusion.ITSystemSimulator.Base;
using CareFusion.ITSystemSimulator.Gui.Pages;
using CareFusion.ITSystemSimulator.Gui.Pages.Utils;
using CareFusion.ITSystemSimulator.Interfaces;
using CareFusion.Lib.StorageSystem;
using CareFusion.Lib.StorageSystem.Attributes;
using CareFusion.Lib.StorageSystem.EventArguments;
using CareFusion.Lib.StorageSystem.Helpers;
using CareFusion.Lib.StorageSystem.Helpers.Enums;
using CareFusion.Lib.StorageSystem.Input;
using CareFusion.Lib.StorageSystem.Output;
using CareFusion.Lib.StorageSystem.Sales;
using CareFusion.Lib.StorageSystem.State;
using CareFusion.Lib.StorageSystem.Stock;
using CareFusion.Lib.StorageSystem.Wwks2.Messages.OutputDestination;
using CareFusion.Lib.StorageSystem.Wwks2.Messages.Hello;
using CareFusion.Lib.StorageSystem.Wwks2.Messages.Input;
using CareFusion.Lib.StorageSystem.Wwks2.Types;
using Microsoft.Win32;
using Rowa.Lib.Log.Extensions;
using Rowa.Mosaic.Shared.ScancodeParsers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace CareFusion.ITSystemSimulator
{
    #region Predefined Delegates

    /// <summary>
    /// Delegate definition for a log entry writer.
    /// </summary>
    /// <param name="format">Format string of the log.</param>
    /// <param name="args">Format arguments of the log.</param>
    public delegate void WriteLog(string format, params object[] args);

    #endregion

    /// <summary>
    /// The IT system simulator main form.
    /// </summary>
    /// <seealso cref="System.Windows.Forms.Form" />
    public partial class FMain : Form
    {
        #region Members

        /// <summary>
        /// Holds the reference to the storage system instance.
        /// </summary>
        private IStorageSystem _storageSystem;

        /// <summary>
        /// Holds the reference to the digital shelf instance.
        /// </summary>
        private IDigitalShelf _digitalShelf = new RowaDigitalShelf();

        /// <summary>
        /// Holds the reference to the digital shelf instance.
        /// </summary>
        private IShoppingCartUpdateRequest _currentShoppingCartUpdateRequest = null;

        /// <summary>
        /// Holds the stock model for the stock overview.
        /// </summary>
        private StockModel _stockModel = new StockModel();

        /// <summary>
        /// Holds the master article data model.
        /// </summary>
        private MasterArticleModel _masterArticleModel = new MasterArticleModel();

        /// <summary>
        /// Holds the stock delivery data model.
        /// </summary>
        private StockDeliveryModel _stockDeliveryModel = new StockDeliveryModel();

        /// <summary>
        /// Holds the output order model for the output overview.
        /// </summary>
        private OrderModel _orderModel;

        /// <summary>
        /// Holds the list of currently active initiated inputs.
        /// </summary>
        private List<IInitiateInputRequest> _activeInitiatedInputs = new List<IInitiateInputRequest>();

        /// <summary>
        /// Holds the list of currently active infeed inputs.
        /// </summary>
        private IInfeedInputRequest _activeInfeedInput;

        /// <summary>
        /// Holds the task information model for the task overview.
        /// </summary>
        private TaskModel _taskModel = new TaskModel();

        /// <summary>
        /// Holds the components status model for the components overview.
        /// </summary>
        private ComponentsModel _componentsModel = new ComponentsModel();

        /// <summary>
        /// Holds the stock locations model for the stock locations overview.
        /// </summary>
        private StockLocationModel _stockLocationModel = new StockLocationModel();

        /// <summary>
        /// Holds the output destination model for the output destination overview.
        /// </summary>
        private OutputDestinationStateIndicationModel _outputDestinationModel = new OutputDestinationStateIndicationModel();

        /// <summary>
        /// The list of articles that are allowed for input.
        /// </summary>
        private InputArticleList _inputArticles = new InputArticleList();

        /// <summary>
        /// The list of code parsers.
        /// </summary>
        private static IScancodeParser[] _codeParserList = new IScancodeParser[4];

        /// <summary>
        /// A flag to determine, if the form is about to close.
        /// </summary>
        private volatile bool _isClosing;

        /// <summary>
        /// A temp string to hold the recent specific maximum sub item quantity on runtime by user. Is used to restore the displayed text when user attemps to leave the control as empty
        /// </summary>
        private string recentSpecificMaxSubItemQuantity;

        /// <summary>
        /// A temp string to hold the recent subscriber id on runtime by user. Is used to restore the displayed text when user attemps to leave the control as empty
        /// </summary>
        private string recentSubscriberId;

        /// <summary>
        /// A temp string to hold the runtime value of a numericUpDown control when user types its value. Is used to restore the displayed text when user attemps to leave the control as empty
        /// </summary>
        private string recentNumberBoxValue;
        private CheckBox newCheckBoxEntryEnableAllCapabilities;
        private Panel allCapabilitiesPanel;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="FMain"/> class.
        /// </summary>
        public FMain()
        {
            InitializeComponent();
            _orderModel = new OrderModel(this);
            _orderModel.PackDispensed += OrderModel_PackDispensed;
            _orderModel.BoxReleased += OrderModel_BoxReleased;
            _codeParserList[0] = ScancodeParserFactory.CreateCodeParser(ScancodeParserType.IFA);
            _codeParserList[1] = ScancodeParserFactory.CreateCodeParser(ScancodeParserType.GS1);
            _codeParserList[2] = ScancodeParserFactory.CreateCodeParser(ScancodeParserType.Singapur);
            _codeParserList[3] = ScancodeParserFactory.CreateCodeParser(ScancodeParserType.Raw);

            comboBoxStateIndication.DataSource = Enum.GetValues(typeof(OutputDestinationState));
        }

        /// <summary>
        /// Handles the Load event of the FMain control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void FMain_Load(object sender, EventArgs e)
        {
            InitGuiElementsMain();
            InitGuiElementsDigitalShelf();
            LoadStoredValuesFromRegistry();
            checkEnDisableInputRespons.CheckState = CheckState.Checked;
        }

        /// <summary>
        /// Initialyse Storage system UI Stuff
        /// </summary>
        private void InitGuiElementsMain()
        {
            SetReadOnlyDataGridOptions(dataGridArticles);
            SetReadOnlyDataGridOptions(dataGridPacks);

            SetReadOnlyDataGridOptions(dataGridOrders);
            SetReadOnlyDataGridOptions(dataGridOrderItems);
            SetReadOnlyDataGridOptions(dataGridItemLabels);
            SetReadOnlyDataGridOptions(dataGridDispensedPacks);
            SetReadOnlyDataGridOptions(dataGridOrderBoxes);

            dataGridArticles.DataSource = _stockModel.Articles;
            dataGridPacks.DataSource = _stockModel.Packs;

            dataGridOrders.DataSource = _orderModel.Orders;
            dataGridOrderItems.DataSource = _orderModel.OrderItems;
            dataGridItemLabels.DataSource = _orderModel.ItemLabels;
            dataGridDispensedPacks.DataSource = _orderModel.DispensedPacks;
            dataGridOrderBoxes.DataSource = _orderModel.OrderBoxes;

            dataGridArticles_SizeChanged(dataGridArticles, null);
            dataGridPacks_SizeChanged(dataGridPacks, null);

            dataGridOrders_SizeChanged(dataGridOrders, null);
            dataGridOrderItems_SizeChanged(dataGridOrderItems, null);
            dataGridItemLabels_SizeChanged(dataGridItemLabels, null);
            dataGridDispensedPacks_SizeChanged(dataGridDispensedPacks, null);

            PrepareMasterArticlesGridDataSource(dataGridMasterArticles, null);

            dataGridMasterArticles.DataSource = _masterArticleModel.MasterArticles;
            dataGridMasterArticles_SizeChanged(dataGridMasterArticles, null);

            dataGridDeliveryItems.DataSource = _stockDeliveryModel.StockDeliveryItems;
            dataGridDeliveryItems_SizeChanged(dataGridDeliveryItems, null);

            SetReadOnlyDataGridOptions(dataGridArticleInfo);
            dataGridArticleInfo.DataSource = _taskModel.Articles;
            dataGridArticleInfo_SizeChanged(dataGridArticleInfo, null);

            SetReadOnlyDataGridOptions(dataGridPackInfo);
            dataGridPackInfo.DataSource = _taskModel.Packs;
            dataGridPackInfo_SizeChanged(dataGridPackInfo, null);

            SetReadOnlyDataGridOptions(dataGridComponents);
            dataGridComponents.DataSource = _componentsModel.Components;
            dataGridComponents_SizeChanged(dataGridComponents, null);

            SetReadOnlyDataGridOptions(dataGridStockLocations);
            dataGridStockLocations.DataSource = _stockLocationModel.StockLocations;
            dataGridStockLocations_SizeChanged(dataGridStockLocations, null);

            dataGridResponses.DataSource = _outputDestinationModel.StateIndicationResponses;         
            dataGridResponses_SizeChanged(dataGridResponses, null);
            dataGridButtonsPressed.DataSource = _outputDestinationModel.ButtonsPressed;
            dataGridButtonsPressed_SizeChanged(dataGridButtonsPressed, null);

            this.InitializeHelloRequestSelectionCapabilities();

            this.InitializeInputResponseSelectionProperties();

            this.LoadCmbInffeedPackExpiryDateSource();

            cmbPackShape.SelectedIndex = 0;
            cmbTaskType.SelectedIndex = 0;
            cmbInfeedPackShape.SelectedIndex = 0;
        }

        #region Connect methods

        /// <summary>
        /// Initializes all properties that the user may select not to be included in <see cref="HelloRequest"/>.
        /// </summary>
        private void InitializeHelloRequestSelectionCapabilities()
        {
            var targetPanel = this.pnlHelloRequestCapabilitiesOptions;

            newCheckBoxEntryEnableAllCapabilities = new CheckBox
            {
                AutoSize = true,
                Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Bold, GraphicsUnit.Point, 0),
                Location = new Point(0, 0),
                Name = $"chkboxSelect_{nameof(HelloRequest)}_Capabilities",
                Text = $"Select capabilities",
                UseVisualStyleBackColor = true,
            };

            newCheckBoxEntryEnableAllCapabilities.CheckedChanged += new EventHandler(this.ChkBoxHelloRequestSelectCapabilities_CheckedChanged);

            targetPanel.Controls.Add(newCheckBoxEntryEnableAllCapabilities);

            var controlsDefaultHeight = newCheckBoxEntryEnableAllCapabilities.Size.Height;
            var controlsDefaultLeftPadding = 15;
            var newControlLocationY = 0;

            allCapabilitiesPanel = new Panel
            {
                AutoSize = true,
                Name = $"pnlAllCapabilities_{nameof(HelloRequest)}",
                Location = new Point(0, newCheckBoxEntryEnableAllCapabilities.Location.Y + controlsDefaultHeight + 5),
                Enabled = false
            };

            var availableCapabilities = ((MosaicSupportedCapabilities[])Enum
                .GetValues(typeof(MosaicSupportedCapabilities)))
                .OrderBy(x => x);

            foreach (var capability in availableCapabilities)
            {
                var selectablePropertyAttribute = (UserSelectablePropertyAttribute)capability.GetType()
                    .GetMember(capability.ToString())
                    .FirstOrDefault()
                    ?.GetCustomAttributes(typeof(UserSelectablePropertyAttribute), false)
                    ?.FirstOrDefault();

                var newCheckBoxEntry = new CheckBox
                {
                    AutoSize = true,
                    Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Bold, GraphicsUnit.Point, 0),
                    Location = new Point(controlsDefaultLeftPadding, newControlLocationY),
                    Name = $"chkboxEnable_{nameof(HelloRequest)}_{capability}",
                    Height = controlsDefaultHeight,
                    Text = $"Enable {capability} capability",
                    UseVisualStyleBackColor = true,
                    Checked = true,
                    Enabled = selectablePropertyAttribute != null
                };

                allCapabilitiesPanel.Controls.Add(newCheckBoxEntry);

                newControlLocationY += newCheckBoxEntry.Height + 2;
            }

            targetPanel.Controls.Add(allCapabilitiesPanel);
        }

        /// <summary>
        /// Handles the CheckedChanged event for the check boxes, through which user selects to enable all properties for an XmlElement of the <see cref="InputResponse"/> message.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        /// <remarks>This does not affect mandatory properties which must always be included in response message</remarks>
        private void ChkBoxHelloRequestSelectCapabilities_CheckedChanged(object sender, EventArgs e)
        {
            if (!(sender is CheckBox targetCheckBox))
            {
                return;
            }

            var controlPrefixValue = targetCheckBox.Name.Substring(0, targetCheckBox.Name.IndexOf('_'));

            var propertyPrefixValue = targetCheckBox.Name.Substring(0, targetCheckBox.Name.LastIndexOf('_'));

            var allCapabilitiesPanelName = propertyPrefixValue.Replace(controlPrefixValue, "pnlAllCapabilities");

            var allCapabilitiesPanel = (Panel)this.Controls.Find(allCapabilitiesPanelName, true).FirstOrDefault();

            allCapabilitiesPanel.Enabled = targetCheckBox.Checked;
        }

        /// <summary>
        /// Gets all capabilities that the user selected to be included on <see cref="HelloRequest"/>.
        /// </summary>
        /// <returns>A <see cref="List{T}"/> of the selected capabilities string values</returns>
        private List<Capability> GetUserSelectedHelloRequestCapabilities()
        {
            var response = new List<Capability>();

            var chkBoxSelectHelloRequestCapabilities = (CheckBox)this.Controls
                .Find($"chkboxSelect_{nameof(HelloRequest)}_Capabilities", true)
                .First();

            var availableCapabilities = ((MosaicSupportedCapabilities[])Enum
                .GetValues(typeof(MosaicSupportedCapabilities)))
                .OrderBy(x => x);

            foreach (var capability in availableCapabilities)
            {
                var chkBoxCapability = (CheckBox)this.Controls
                    .Find($"chkboxEnable_{nameof(HelloRequest)}_{capability}", true)
                    .First();

                var includeCapabilityInResponse = !chkBoxSelectHelloRequestCapabilities.Checked || chkBoxCapability.Checked;

                if (includeCapabilityInResponse)
                {
                    response.Add(new Capability
                    {
                        Name = capability.ToString()
                    });
                }
            }

            return response;
        }

        #endregion

        #region Input tab methods

        /// <summary>
        /// Initializes all properties that the user may select not to be included in <see cref="InputResponse"/>.
        /// </summary>
        private void InitializeInputResponseSelectionProperties()
        {
            var inputRequestCoreProperties = typeof(InputRequest).GetProperties();
            var inputRequestArticleProperties = typeof(Article).GetProperties();
            var inputRequestPackProperties = typeof(Pack).GetProperties();

            this.InitializeInputResponseProperties<InputResponse>(this.pnlInputResponseCoreOptions, inputRequestCoreProperties);

            this.InitializeInputResponseProperties<Article>(this.pnlInputResponseArticleOptions, inputRequestArticleProperties);

            this.InitializeInputResponseProperties<Pack>(this.pnlInputResponsePackOptions, inputRequestPackProperties);

            this.InitializeInputResponseProperties<Handling>(this.pnlInputResponsePackHandlingOptions, default);
        }

        /// <summary>
        /// Attaches to each inner input tab the proper fields that the user may select to include in <see cref="InputResponse"/>.
        /// </summary>
        /// <typeparam name="T">The type of class that its properties must be displayed to user for selection</typeparam>
        /// <param name="inputRequestProperties">An <see cref="Array"/> of properties that <see cref="InputRequest"/> contains.
        /// For those that are not common, user will not have an option to copy its value between them.</param>
        /// <param name="targetPanel">The parent panel in which fields are attached.</param>
        /// <remarks>If a property is mandatory user will not have an option to disable it. Only overwrite its default value</remarks>
        private void InitializeInputResponseProperties<T>(Panel targetPanel, PropertyInfo[] inputRequestProperties)
        {
            Debug.Assert(targetPanel != null);

            var targetProperties = typeof(T).GetProperties()
                .Where(prop => Attribute.IsDefined(prop, typeof(UserSelectablePropertyAttribute)))
                .OrderBy(i => i.Name);

            var newCheckBoxEntryEnableAllProperties = new CheckBox
            {
                AutoSize = true,
                Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Bold, GraphicsUnit.Point, 0),
                Location = new Point(0, 0),
                Name = $"chkboxSelect_{typeof(T).Name}_Properties",
                Text = $"Select properties",
                UseVisualStyleBackColor = true
            };

            targetPanel.Controls.Add(newCheckBoxEntryEnableAllProperties);

            newCheckBoxEntryEnableAllProperties.CheckedChanged += new EventHandler(this.ChkBoxInputResponseSelectProperties_CheckedChanged);

            var controlsDefaultHeight = newCheckBoxEntryEnableAllProperties.Size.Height;
            var controlsDefaultLeftPadding = 15;
            var newControlLocationY = 0;

            var allPropertiesPanel = new Panel
            {
                AutoSize = true,
                Name = $"pnlAllProperties_{typeof(T).Name}",
                Location = new Point(0, newCheckBoxEntryEnableAllProperties.Location.Y + controlsDefaultHeight + 5),
                Enabled = false
            };

            foreach (var property in targetProperties)
            {
                var newCheckBoxEntry = new CheckBox
                {
                    AutoSize = true,
                    Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Bold, GraphicsUnit.Point, 0),
                    Location = new Point(controlsDefaultLeftPadding, newControlLocationY),
                    Name = $"chkboxEnable_{typeof(T).Name}_{property.Name}",
                    Height = controlsDefaultHeight,
                    Text = $"Enable {property.Name} property",
                    UseVisualStyleBackColor = true,
                    Checked = true
                };

                newCheckBoxEntry.CheckedChanged += new EventHandler(this.ChkBoxInputResponsePropertyEnable_CheckedChanged);

                allPropertiesPanel.Controls.Add(newCheckBoxEntry);

                newControlLocationY += newCheckBoxEntry.Height + 2;

                var propertyPanelPreperationResult = this.PrepareInputResponsePropertyOverridePanelControls<T>(property,
                    new Point(controlsDefaultLeftPadding, newControlLocationY),
                    allPropertiesPanel.Width,
                    controlsDefaultLeftPadding,
                    controlsDefaultHeight, inputRequestProperties);

                if (propertyPanelPreperationResult.isMandatory)
                {
                    newCheckBoxEntry.Enabled = false;

                    newCheckBoxEntry.CheckedChanged -= new EventHandler(this.ChkBoxInputResponsePropertyEnable_CheckedChanged);
                }

                var newPanelEntry = propertyPanelPreperationResult.panel;

                allPropertiesPanel.Controls.Add(newPanelEntry);

                newControlLocationY += newPanelEntry.Height + 2;
            }

            targetPanel.Controls.Add(allPropertiesPanel);
        }

        /// <summary>
        /// Prepares an inner panel holding all controls required for the user to decide the value of a property inside <see cref="InputResponse"/> message.
        /// The inner pannel contains the following:
        /// 1. A <see cref="RadioButton"/> when checked property's value will be the default one.
        /// 2. A <see cref="RadioButton"/> when checked property's value will be set by user
        /// 3. An appropriate control (<see cref="TextBox"/>, <see cref="NumericUpDown"/>, <see cref="DateTimePicker"/> etc) to specify the property's value.
        /// </summary>
        /// <typeparam name="T">The type of examined property</typeparam>
        /// <param name="property">The property.</param>
        /// <param name="panelPoint">The panel's <see cref="Point"/> options inside its parent.</param>
        /// <param name="panelWidth">The panel's width.</param>
        /// <param name="innerControlsStartLocationX">The inner controls start location x.</param>
        /// <param name="innerControlsDefaultHeight">Default height of the inner controls.</param>
        /// <param name="inputRequestProperties">An <see cref="Array"/> of properties that <see cref="InputRequest"/> contains.
        /// For those that are not common, user will not have an option to copy its value between them.</param>
        /// <returns>A <see cref="Panel"/, including all above specified controls></returns>
        private (Panel panel, bool isMandatory) PrepareInputResponsePropertyOverridePanelControls<T>(PropertyInfo property, Point panelPoint, int panelWidth,
            int innerControlsStartLocationX, int innerControlsDefaultHeight, PropertyInfo[] inputRequestProperties)
        {
            Debug.Assert(panelPoint != null);

            var innerControlsLocationY = 0;

            var propertySelectableAtribute = (UserSelectablePropertyAttribute)property.GetCustomAttributes(typeof(UserSelectablePropertyAttribute)).First();

            var propertyExistsInInputRequest = inputRequestProperties?.Any(p => p.Name.Equals(property.Name)) ?? false;

            var response = new Panel
            {
                AutoSize = true,
                Name = $"pnlPropertyValueOptions_{typeof(T).Name}_{property.Name}",
                Location = panelPoint,
                Size = new Size
                {
                    Width = panelWidth,
                    Height = propertyExistsInInputRequest ? innerControlsDefaultHeight * 3 : innerControlsDefaultHeight * 2
                }
            };

            var defaultValueRadioButton = new RadioButton
            {
                AutoSize = true,
                Name = $"rbtnDefaultValue_{typeof(T).Name}_{property.Name}",
                Location = new Point(innerControlsStartLocationX, innerControlsLocationY),
                Height = innerControlsDefaultHeight,
                Text = "Send default value",
                TextAlign = ContentAlignment.MiddleLeft,
                Checked = true,
                UseVisualStyleBackColor = true
            };

            defaultValueRadioButton.CheckedChanged += new EventHandler(this.RadioButtonInputResponsePropertyValue_CheckedChanged);

            response.Controls.Add(defaultValueRadioButton);

            innerControlsLocationY += innerControlsDefaultHeight;

            if (propertyExistsInInputRequest)
            {
                var inputRequestValueRadioButton = new RadioButton
                {
                    AutoSize = true,
                    Name = $"rbtnInputRequestValue_{typeof(T).Name}_{property.Name}",
                    Location = new Point(innerControlsStartLocationX, innerControlsLocationY),
                    Height = innerControlsDefaultHeight,
                    Text = "Send back input request value (if exists)",
                    TextAlign = ContentAlignment.MiddleLeft,
                    UseVisualStyleBackColor = true
                };

                inputRequestValueRadioButton.CheckedChanged += new EventHandler(this.RadioButtonInputResponsePropertyValue_CheckedChanged);

                response.Controls.Add(inputRequestValueRadioButton);

                innerControlsLocationY += innerControlsDefaultHeight;
            }

            var customValueRadioButton = new RadioButton
            {
                AutoSize = true,
                Name = $"rbtnCustomValue_{typeof(T).Name}_{property.Name}",
                Location = new Point(innerControlsStartLocationX, innerControlsLocationY + 3),
                Height = innerControlsDefaultHeight,
                UseVisualStyleBackColor = true
            };

            customValueRadioButton.Height = innerControlsDefaultHeight;

            customValueRadioButton.CheckedChanged += new EventHandler(this.RadioButtonInputResponsePropertyValue_CheckedChanged);

            response.Controls.Add(customValueRadioButton);

            var propertyValueOverrideControl = this.PrepareInputResponsePropertyOverrideValueControl<T>(property, propertySelectableAtribute,
                 new Point(innerControlsStartLocationX * 2, innerControlsLocationY),
                 new Size(response.Width, innerControlsDefaultHeight));

            response.Controls.Add(propertyValueOverrideControl);

            return (panel: response, isMandatory: propertySelectableAtribute.IsMandatory);
        }

        /// <summary>
        /// Prepares the control to override the value of a property that user has selected to include in <see cref="InputResponse"/> message.
        /// </summary>
        /// <typeparam name="T">The type of property to be overriden</typeparam>
        /// <param name="property">The property.</param>
        /// <param name="selectablePropertyAttribute"></param>
        /// <param name="point">The point.</param>
        /// <param name="size">The size.</param>
        /// <returns>A control</returns>
        private Control PrepareInputResponsePropertyOverrideValueControl<T>(PropertyInfo property, UserSelectablePropertyAttribute selectablePropertyAttribute, Point point, Size size)
        {
            Debug.Assert(property != null);
            Debug.Assert(selectablePropertyAttribute != null);
            Debug.Assert(point != null);
            Debug.Assert(size != null);

            var numericTypes = new List<Type>()
            {
                typeof(int),
                typeof(double),
                typeof(decimal),
                typeof(long),
                typeof(short),
                typeof(sbyte),
                typeof(byte),
                typeof(ulong),
                typeof(ushort),
                typeof(uint),
                typeof(float)
            };

            if (numericTypes.Any(t => t.Name.Equals(selectablePropertyAttribute.PropertyType.Name)))
            {
                var response = new NumericUpDown
                {
                    AutoSize = true,
                    Name = $"numBoxCustomValue_{typeof(T).Name}_{property.Name}",
                    Location = point,
                    Size = size,
                    Minimum = 0,
                    Maximum = 1000,
                    Enabled = false
                };

                response.KeyUp += new KeyEventHandler(this.NumboxInputResponsePropertyValueOverride_KeyUp);

                response.Leave += new EventHandler(this.NumboxInputResponsePropertyValueOverride_Leave);

                return response;
            }

            if (selectablePropertyAttribute.PropertyType.Name.Equals(typeof(DateTime).Name))
            {
                return new DateTimePicker
                {
                    AutoSize = true,
                    Name = $"dateTimeCustomValue_{typeof(T).Name}_{property.Name}",
                    Location = point,
                    Size = size,
                    Enabled = false,
                    Format = DateTimePickerFormat.Custom,
                    CustomFormat = "yyyy-MM-dd",
                    MinDate = DateTime.MinValue
                };
            }

            if (selectablePropertyAttribute.PropertyType.Name.Equals(typeof(bool).Name))
            {
                var response = new ComboBox
                {
                    AutoSize = true,
                    Name = $"comboBoxCustomValue_{typeof(T).Name}_{property.Name}",
                    Location = point,
                    Size = size,
                    Enabled = false,
                    DataSource = new List<ComboboxItem>
                    {
                        new ComboboxItem("No","False"),
                        new ComboboxItem("Yes","True")
                    }
                };

                return response;
            }

            if (selectablePropertyAttribute.PropertyType.IsEnum)
            {
                var dataSource = Enum.GetValues(selectablePropertyAttribute.PropertyType).OfType<Enum>()
                    .Select(enumItem =>
                    {
                        var displayName = enumItem.GetType()
                            .GetMember(enumItem.ToString())
                            .FirstOrDefault()
                            .GetCustomAttribute<DescriptionAttribute>()?.Description ?? enumItem.ToString();

                        return new ComboboxItem(displayName, enumItem.ToString());
                    })
                    .OrderBy(x => x.Text)
                    .ToArray();

                return new ComboBox
                {
                    AutoSize = true,
                    Name = $"comboBoxCustomValue_{typeof(T).Name}_{property.Name}",
                    Location = point,
                    Size = size,
                    Enabled = false,
                    DataSource = dataSource
                };
            }

            return new TextBox
            {
                AutoSize = true,
                Name = $"txtboxCustomValue_{typeof(T).Name}_{property.Name}",
                Location = point,
                Size = size,
                Enabled = false
            };
        }

        /// <summary>
        /// Handles the KeyUp event on the numBoxes used to override input response value for a property.
        /// It keeps the recent valid value for the control in order to be restored iff a user leaves it empty on leave event
        /// </summary>
        /// <param name="sender">Infeed request which triggered the event.</param>
        /// <param name="e">KeyEvent parameter which are not used here.</param>
        private void NumboxInputResponsePropertyValueOverride_KeyUp(object sender, KeyEventArgs e)
        {
            var targetControl = (NumericUpDown)sender;

            try
            {
                if (!string.IsNullOrEmpty(targetControl.Text))
                {
                    Convert.ToInt16(targetControl.Text);

                    recentNumberBoxValue = targetControl.Text;
                }
            }
            catch
            {
                targetControl.Text = recentNumberBoxValue;

                targetControl.Select(targetControl.Text.Length, 0);
            }
        }

        /// <summary>
        /// Handles the Leave event on the numBoxes used to override input response value for a property.
        /// Iff text value remains empty, it is set to the recent one, that is kept by Keyup event
        /// </summary>
        /// <param name="sender">Infeed request which triggered the event.</param>
        /// <param name="e">Event parameter which are not used here.</param>
        private void NumboxInputResponsePropertyValueOverride_Leave(object sender, EventArgs e)
        {
            var targetControl = (NumericUpDown)sender;

            if (string.IsNullOrEmpty(targetControl.Text))
            {
                targetControl.Text = recentNumberBoxValue.ToString();
            }
        }

        /// <summary>
        /// Handles the CheckedChanged event for the check boxes, through which user selects a property to be included in <see cref="InputResponse"/> message.
        /// Whenever checked radio buttons for setting default or custom value to property, are enabled, otherwise disabled.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void ChkBoxInputResponsePropertyEnable_CheckedChanged(object sender, EventArgs e)
        {
            if (!(sender is CheckBox targetCheckBox))
            {
                return;
            }

            var senderPrefixName = targetCheckBox.Name.Substring(0, targetCheckBox.Name.IndexOf('_'));

            var targetPanelName = targetCheckBox.Name.Replace(senderPrefixName, "pnlPropertyValueOptions");

            var customValueRadioButtonName = targetCheckBox.Name.Replace(senderPrefixName, "rbtnCustomValue");

            var targetPanel = targetCheckBox.Parent.Controls
                .OfType<Panel>()
                .FirstOrDefault(c => c.Name.Equals(targetPanelName));

            if (targetPanel == null)
            {
                return;
            }

            foreach (var rbtnControl in targetPanel.Controls.OfType<RadioButton>())
            {
                rbtnControl.Enabled = targetCheckBox.Checked;

                if (rbtnControl.Name.Equals(customValueRadioButtonName))
                {
                    targetPanel.Controls.OfType<Control>()
                        .Where(c => !c.GetType().Name.Equals(nameof(RadioButton)))
                        .ToList()
                        .ForEach(c => c.Enabled = targetCheckBox.Checked && rbtnControl.Checked);
                }
            }
        }

        /// <summary>
        /// Handles the CheckedChanged event for the check boxes, through which user selects to enable all properties for an XmlElement of the <see cref="InputResponse"/> message.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        /// <remarks>This does not affect mandatory properties which must always be included in response message</remarks>
        private void ChkBoxInputResponseSelectProperties_CheckedChanged(object sender, EventArgs e)
        {
            if (!(sender is CheckBox targetCheckBox))
            {
                return;
            }

            var controlPrefixValue = targetCheckBox.Name.Substring(0, targetCheckBox.Name.IndexOf('_'));

            var propertyPrefixValue = targetCheckBox.Name.Substring(0, targetCheckBox.Name.LastIndexOf('_'));

            var allPropertiesPanelName = propertyPrefixValue.Replace(controlPrefixValue, "pnlAllProperties");

            var allPropertiesPanel = (Panel)this.Controls.Find(allPropertiesPanelName, true).FirstOrDefault();

            allPropertiesPanel.Enabled = targetCheckBox.Checked;
        }

        /// <summary>
        /// Handles the CheckedChanged event for radio buttons that handle the value of property user has selected to be included in <see cref="InputResponse"/> message.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void RadioButtonInputResponsePropertyValue_CheckedChanged(object sender, EventArgs e)
        {
            if (!(sender is RadioButton targetRadioButton))
            {
                return;
            }

            var enableOverrideValueControsl = targetRadioButton.Checked && targetRadioButton.Name.Contains("rbtnCustomValue_");

            targetRadioButton.Parent.Controls.OfType<Control>()
                .Where(c => !c.GetType().Name.Equals(nameof(RadioButton)))
                .ToList()
                .ForEach(c => c.Enabled = enableOverrideValueControsl);
        }

        private List<UserInputResponsePropertyOptions> GetInputResponsePackHandlingOptions()
        {
            var inputResponsePackHandlingOptions = default(List<UserInputResponsePropertyOptions>);
            var chkBoxInputResponsePackHandlingProperties = (CheckBox)this.Controls
                .Find($"chkboxSelect_{nameof(Handling)}_Properties", true)
                .FirstOrDefault();
            if (chkBoxInputResponsePackHandlingProperties.Checked)
            {
                var targetPanel = (Panel)this.Controls.Find($"pnlAllProperties_{nameof(Handling)}", true).First();
                inputResponsePackHandlingOptions = this.GetUserSelectedInputResponseElements(targetPanel);
            }

            return inputResponsePackHandlingOptions;
        }

        private List<UserInputResponsePropertyOptions> GetInputResponsePackOptions()
        {
            var inputResponsePackOptions = default(List<UserInputResponsePropertyOptions>);
            var chkBoxInputResponsePackProperties = (CheckBox)this.Controls
                .Find($"chkboxSelect_{nameof(Pack)}_Properties", true)
                .FirstOrDefault();

            if (chkBoxInputResponsePackProperties.Checked)
            {
                var targetPanel = (Panel)this.Controls.Find($"pnlAllProperties_{nameof(Pack)}", true).First();
                inputResponsePackOptions = this.GetUserSelectedInputResponseElements(targetPanel);
            }

            return inputResponsePackOptions;
        }

        private List<UserInputResponsePropertyOptions> GetInputResponseArticleOptions()
        {
            var inputResponseArticleOptions = default(List<UserInputResponsePropertyOptions>);
            var chkBoxInputResponseArticleProperties = (CheckBox)this.Controls
                .Find($"chkboxSelect_{nameof(Article)}_Properties", true)
                .FirstOrDefault();

            if (chkBoxInputResponseArticleProperties.Checked)
            {
                var targetPanel = (Panel)this.Controls.Find($"pnlAllProperties_{nameof(Article)}", true).First();
                inputResponseArticleOptions = this.GetUserSelectedInputResponseElements(targetPanel);
            }

            return inputResponseArticleOptions;
        }

        private List<UserInputResponsePropertyOptions> GetInputResponseCoreOptions()
        {
            var inputResponseCoreOptions = default(List<UserInputResponsePropertyOptions>);
            var chkBoxInputResponseCoreProperties = (CheckBox)this.Controls
                .Find($"chkboxSelect_{nameof(InputResponse)}_Properties", true)
                .FirstOrDefault();

            if (chkBoxInputResponseCoreProperties.Checked)
            {
                var targetPanel = (Panel)this.Controls.Find($"pnlAllProperties_{nameof(InputResponse)}", true).First();
                inputResponseCoreOptions = this.GetUserSelectedInputResponseElements(targetPanel);
            }

            return inputResponseCoreOptions;
        }

        /// <summary>
        /// Gets user's options about how properties of an input response xml element will be handled.
        /// </summary>
        /// <param name="parentPanel">The parent panel which contains all related controls that clarify properties value.</param>
        /// <returns>A <see cref="List{T}"/> of <see cref="UserInputResponsePropertyOptions"/></returns>
        private List<UserInputResponsePropertyOptions> GetUserSelectedInputResponseElements(Panel parentPanel)
        {
            Debug.Assert(parentPanel != null);

            var response = new List<UserInputResponsePropertyOptions>();

            foreach (var targetCheckBox in parentPanel.Controls.OfType<CheckBox>().Where(i => i.Checked))
            {
                var controlPrefixValue = targetCheckBox.Name.Substring(0, targetCheckBox.Name.IndexOf('_'));

                var propertyPrefixValue = targetCheckBox.Name.Substring(0, targetCheckBox.Name.LastIndexOf('_') + 1);

                var propertyValueOptionsPanelName = targetCheckBox.Name.Replace(controlPrefixValue, "pnlPropertyValueOptions");

                var propertyValueOptionsPanel = targetCheckBox.Parent.Controls
                    .OfType<Panel>()
                    .FirstOrDefault(p => p.Name.Equals(propertyValueOptionsPanelName));

                if (propertyValueOptionsPanel == null)
                {
                    continue;
                }

                var targetRadioButton = propertyValueOptionsPanel.Controls.OfType<RadioButton>()
                    .FirstOrDefault(r => r.Checked);

                var propertyOptions = new UserInputResponsePropertyOptions
                {
                    PropertyName = targetCheckBox.Name.Replace(propertyPrefixValue, string.Empty)
                };

                if (targetRadioButton.Name.Contains("rbtnDefaultValue_"))
                {
                    propertyOptions.UseDefaultValue = true;

                    response.Add(propertyOptions);

                    continue;
                }

                if (targetRadioButton.Name.Contains("rbtnInputRequestValue_"))
                {
                    propertyOptions.UseInputRequestValue = true;
                    
                    response.Add(propertyOptions);

                    continue;
                }

                var propertyValueControl = propertyValueOptionsPanel.Controls.OfType<Control>()
                    .FirstOrDefault(c => !c.GetType().Name.Equals(nameof(RadioButton)));

                if (propertyValueControl == null)
                {
                    response.Add(propertyOptions);

                    continue;
                }

                switch (propertyValueControl.GetType().Name)
                {
                    case nameof(TextBox):
                        propertyOptions.CustomValue = ((TextBox)propertyValueControl).Text;
                        break;
                    case nameof(NumericUpDown):
                        propertyOptions.CustomValue = ((NumericUpDown)propertyValueControl).Value.ToString();
                        break;
                    case nameof(ComboBox):
                        {
                            var selectedValue = !(((ComboBox)propertyValueControl).SelectedValue is ComboboxItem comboboxItem)
                                ? (string)((ComboBox)propertyValueControl).SelectedValue
                                : comboboxItem.Value;

                            propertyOptions.CustomValue = selectedValue;

                            break;
                        }
                    case nameof(DateTimePicker):
                        propertyOptions.CustomValue = ((DateTimePicker)propertyValueControl).Value.ToString();
                        break;
                }

                response.Add(propertyOptions);
            }

            return response;
        }

        /// <summary>
        /// Updates the values of the provided object.
        /// This is used during preparation of <see cref="InputResponse"/> message.
        /// Iff user has selected specific properties to be serialized with specific value, those values are assigned to corresponding object's properties
        /// </summary>
        /// <param name="targetObject">The object to update, whenever required, its values</param>
        /// <param name="initialRequestObject">The object received from input request and iff selected by user, its value will be send back through response message</param>
        /// <param name="responseObjectOptions">The <see cref="UserInputResponsePropertyOptions"/> for this object.</param>
        private void ModifyInputResponseObject(object targetObject, object initialRequestObject,
            List<UserInputResponsePropertyOptions> responseObjectOptions)
        {
            Debug.Assert(targetObject != null);
            Debug.Assert(responseObjectOptions != null);

            if (initialRequestObject == null)
            {
                initialRequestObject = targetObject;
            }

            var targetObjectProperties = responseObjectOptions
                .Select(i => i.PropertyName).ToList();

            var targetProperties = targetObject.GetType()
                .GetProperties()
                .Where(prop => Attribute.IsDefined(prop, typeof(UserSelectablePropertyAttribute)))
                .ToList();

            var inputRequestObjectProperties = initialRequestObject?.GetType().GetProperties();

            foreach (var targetProperty in targetProperties)
            {
                if (!targetObjectProperties.Any(s => s.Equals(targetProperty.Name)))
                {
                    targetProperty.SetValue(targetObject, default);

                    continue;
                }

                var userPropertyOptions = responseObjectOptions
                    .First(i => i.PropertyName.Equals(targetProperty.Name));

                if (userPropertyOptions.UseDefaultValue)
                {
                    continue;
                }

                var overwrittenStringValue = default(string);

                if (userPropertyOptions.UseInputRequestValue)
                {
                    Debug.Assert(inputRequestObjectProperties != null);

                    var inputRequestProperty = inputRequestObjectProperties
                        .FirstOrDefault(p => p.Name.Equals(targetProperty.Name));

                    if (inputRequestProperty == null)
                    {
                        continue;
                    }

                    overwrittenStringValue = inputRequestProperty.GetValue(initialRequestObject)?.ToString();
                }
                else
                {
                    overwrittenStringValue = userPropertyOptions.CustomValue;
                }

                var overwrittenValue = targetProperty.PropertyType.IsEnum
                    ? Enum.ToObject(targetProperty.PropertyType, Convert.ToInt32(overwrittenStringValue))
                    : Convert.ChangeType(overwrittenStringValue, targetProperty.PropertyType);

                targetProperty.SetValue(targetObject, overwrittenValue);
            }
        }

        #endregion

        #region InfeedInput tab methods

        /// <summary>
        /// Loads the inffeed pack expiry date source combo box with all its available values.
        /// </summary>
        private void LoadCmbInffeedPackExpiryDateSource()
        {
            this.cmbInfeedPackExpiryDateSource.DataSource = Enum.GetValues(typeof(ExpiryDateSource)).OfType<ExpiryDateSource>()
                .Select(enumItem =>
                {
                    var displayName = enumItem.GetType()
                        .GetMember(enumItem.ToString())
                        .FirstOrDefault()
                        .GetCustomAttribute<DescriptionAttribute>()?.Description;

                    return new ComboboxItem(displayName ?? enumItem.ToString(), Convert.ChangeType(enumItem, typeof(ExpiryDateSource)).ToString());
                })
                .OrderBy(x => x.Text)
                .ToArray();
        }

        #endregion

        #region MasterArticle tab methods

        /// <summary>
        /// Prepares the master articles grid as follows:
        /// 1. Sets the <see cref="MasterArticleModel"/> as its DataSource
        /// 2. Adds a new button column at the end of each row to set productCodes.
        /// 3. Raises the <see cref="dataGridArticleInfo_SizeChanged(object, EventArgs)"/>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PrepareMasterArticlesGridDataSource(object sender, EventArgs e)
        {
            dataGridMasterArticles.DataSource = null;

            if (dataGridMasterArticles.Columns.Contains("btnProductCodes"))
            {
                dataGridMasterArticles.Columns.Remove("btnProductCodes");
            }

            dataGridMasterArticles.DataSource = _masterArticleModel.MasterArticles;
            dataGridMasterArticles.Columns["ProductCodes"].ReadOnly = true;

            var productCodesColumnIndex = dataGridMasterArticles.Columns.IndexOf(dataGridMasterArticles.Columns["ProductCodes"]);

            dataGridMasterArticles.Columns.Insert(productCodesColumnIndex + 1, new DataGridViewButtonColumn
            {
                Name = "btnProductCodes",
                HeaderText = "Add/Edit product codes",
                Text = "Add/Edit",
                UseColumnTextForButtonValue = true
            });

            dataGridMasterArticles_SizeChanged(sender, e);
        }

        /// <summary>
        /// Handles dataGridMasterArticles cell click event
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridMasterArticles_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == dataGridMasterArticles.Columns["btnProductCodes"].Index)
            {
                var serializedProductCodes = dataGridMasterArticles.Rows[e.RowIndex].Cells["ProductCodes"].Value.ToString();

                var frmMasterArticleProductCodes = new FormMasterArticleProductCodes(serializedProductCodes);

                if (frmMasterArticleProductCodes.ShowDialog() == DialogResult.OK)
                {
                    dataGridMasterArticles.Columns["ProductCodes"].ReadOnly = false;
                    dataGridMasterArticles.CurrentRow.Cells["ProductCodes"].Value = frmMasterArticleProductCodes.GetSerializedProductCodes();
                    dataGridMasterArticles.Columns["ProductCodes"].ReadOnly = true;
                }
            }
        }

        /// <summary>
        /// Handles the SizeChanged event of the dataGridMasterArticles control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridMasterArticles_SizeChanged(object sender, EventArgs e)
        {
            if (dataGridMasterArticles.Columns.Count >= 10)
            {
                dataGridMasterArticles.Columns[2].Width = 80;
                dataGridMasterArticles.Columns[3].Width = 80;
                dataGridMasterArticles.Columns[4].Width = 80;
                dataGridMasterArticles.Columns[5].Width = 80;
                dataGridMasterArticles.Columns[8].Width = 80;
                dataGridMasterArticles.Columns[9].Width = 80;


                dataGridMasterArticles.Columns[0].Width = (dataGridMasterArticles.Width - 550) / 4;
                dataGridMasterArticles.Columns[1].Width = dataGridMasterArticles.Columns[0].Width;
                dataGridMasterArticles.Columns[6].Width = dataGridMasterArticles.Columns[0].Width;
                dataGridMasterArticles.Columns[7].Width = dataGridMasterArticles.Columns[0].Width;

                /*for (int i = 1; i < 8; ++i)
                {
                    dataGridMasterArticles.Columns[i].Width = dataGridMasterArticles.Columns[0].Width;
                }*/
            }
        }

        /// <summary>
        /// Update dataGridMasterArticlesDisplay when selection change
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridMasterArticles_SelectionChanged(object sender, EventArgs e)
        {
            UpdateDataGridMasterArticlesDisplay();
        }

        /// <summary>
        /// Update dataGridMasterArticlesDisplay when selection change
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridMasterArticles_RowEnter(object sender, DataGridViewCellEventArgs e)
        {
            UpdateDataGridMasterArticlesDisplay();
        }

        /// <summary>
        /// Update dataGridMasterArticlesDisplay when selection change
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridMasterArticles_CellEnter(object sender, DataGridViewCellEventArgs e)
        {
            UpdateDataGridMasterArticlesDisplay();
        }

        /// <summary>
        /// Update dataGridMasterArticlesDisplay when selection change
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void UpdateDataGridMasterArticlesDisplay()
        {
            int? selectedRowIndex = null;
            if (dataGridMasterArticles.SelectedCells != null)
            {
                foreach (DataGridViewCell selectCell in dataGridMasterArticles.SelectedCells)
                {
                    if (!selectedRowIndex.HasValue)
                    {
                        selectedRowIndex = selectCell.RowIndex;
                    }
                    else
                    {
                        if (selectedRowIndex.Value != selectCell.RowIndex)
                        {
                        }
                    }
                }
            }

            if (dataGridMasterArticles.SelectedRows != null)
            {
                foreach (DataGridViewRow selectRow in dataGridMasterArticles.SelectedRows)
                {
                    if (!selectedRowIndex.HasValue)
                    {
                        selectedRowIndex = selectRow.Index;
                    }
                    else
                    {
                        if (selectedRowIndex.Value != selectRow.Index)
                        {
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the btnSendMasterArticles control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnSendMasterArticles_Click(object sender, EventArgs e)
        {
            try
            {
                btnSendMasterArticles.Enabled = false;
                _masterArticleModel.Send(_storageSystem);
            }
            catch (Exception ex)
            {
                var msg = string.Format("Updating master articles failed!\n\n{0}", ex.Message);
                MessageBox.Show(msg, "IT System Simulator");
            }

            btnSendMasterArticles.Enabled = true;
        }

        /// <summary>
        /// Handles the Click event of the btnGenerateArticles control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnGenerateArticles_Click(object sender, EventArgs e)
        {
            _masterArticleModel.GenerateMasterArticles();

            PrepareMasterArticlesGridDataSource(sender, e);

            dataGridMasterArticles.Refresh();
        }

        /// <summary>
        /// Handles the Click event of the btnImportSelectArticles control.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Dispensing event args.</param>
        private void btnImportSelectArticles_Click(object sender, EventArgs e)
        {
            if (openArticleFileDialog.ShowDialog() != DialogResult.OK)
                return;

            dataGridMasterArticles.DataSource = null;
            dataGridMasterArticles.Refresh();
            _masterArticleModel.GenerateFromSelectArticleFile(openArticleFileDialog.FileName);
            dataGridMasterArticles.DataSource = _masterArticleModel.MasterArticles;
            dataGridMasterArticles.Refresh();
        }

        #endregion

        /// <summary>
        /// Initialyse Digital shelf UI Stuff
        /// </summary>
        private void InitGuiElementsDigitalShelf()
        {
            comboBoxShoppingCartStatus.Items.Add(ShoppingCartStatus.Active);
            comboBoxShoppingCartStatus.Items.Add(ShoppingCartStatus.Finished);
            comboBoxShoppingCartStatus.Items.Add(ShoppingCartStatus.Discarded);
            comboBoxShoppingCartStatus.SelectedIndex = 0;

            comboBoxShoppingCartInfoStatus.Items.Add(string.Empty);
            comboBoxShoppingCartInfoStatus.Items.Add(ShoppingCartStatus.Active);
            comboBoxShoppingCartInfoStatus.Items.Add(ShoppingCartStatus.Finished);
            comboBoxShoppingCartInfoStatus.Items.Add(ShoppingCartStatus.Discarded);
            comboBoxShoppingCartInfoStatus.SelectedIndex = 0;

            checkBoxTagAutoGenerate_Click(null, null);
            checkBoxCrossSellingArticleAutoGenerate_Click(null, null);
            checkBoxAlternativeArticlesAutoGenerate_Click(null, null);
            checkBoxAlternativePackSizeAutoGenerated_Click(null, null);
            checkBoxPriceInformationAutoGenerate_Click(null, null);
            checkBoxArticleInformationAutoGenerate_Click(null, null);
        }

        /// <summary>
        /// Load configuration from Registry
        /// </summary>
        private void LoadStoredValuesFromRegistry()
        {
            using (var key = Registry.CurrentUser.CreateSubKey("Software\\CareFusion\\ITSystemSimulator"))
            {
                var address = (string)key.GetValue("StorageSystemAddress");
                var port = key.GetValue("StorageSystemPort");
                var autoconnect = (string)key.GetValue("AutoConnectEnable");
                var allowdeliveryinput = (string)key.GetValue("AllowDeliveryInput");
                var allowstockretinput = (string)key.GetValue("AllowStockReturnInput");
                var onlyfridgeinput = (string)key.GetValue("OnlyFridgeInput");
                var onlyarticlesfromlist = (string)key.GetValue("OnlyArticlesFromList");
                var enforcepickingindicator = (string)key.GetValue("EnforcePickingIndicator");
                var enforceexpirydatereturn = (string)key.GetValue("EnforceExpiryDateStockReturn");
                var enforceexpirydatedelivery = (string)key.GetValue("EnforceExpiryDateDelivery");
                var enforcebatchreturn = (string)key.GetValue("EnforceBatchStockReturn");
                var enforcebatchdelivery = (string)key.GetValue("EnforceBatchDelivery");
                var enforcelocationreturn = (string)key.GetValue("EnforceLocationStockReturn");
                var enforcelocationnewdelivery = (string)key.GetValue("EnforceLocationNewDelivery");
                var enforceSerialNumberStockReturn = (string)key.GetValue("EnforceSerialNumberStockReturn");
                var enforceSerialNumberNewDelivery = (string)key.GetValue("EnforceSerialNumberNewDelivery");
                var parseDatamatrixCodes = (string)key.GetValue("ParseDatamatrixCodes");
                var setMaxSubItemQuantity = (string)key.GetValue("SetMaxSubItemQuantity");
                var setRandomMaxSubItemQuantity = (string)key.GetValue("SetRandomMaxSubItemQuantity");
                var randomMaxSubItemQuantity = (string)key.GetValue("RandomMaxSubItemQuantity");
                var boxsetvirtualarticle = (string)key.GetValue("BoxSetVirtualArticle");
                var boxaddotherrobotarticle = (string)key.GetValue("BoxAddOtherRobotArticle");
                var overwriteArticleName = (string)key.GetValue("OverwriteArticleName");
                var overwriteArticleNameText = (string)key.GetValue("OverwriteArticleNameText");
                var overwritelocation = (string)key.GetValue("OverwriteStockLocation");
                var overwritelocationtext = (string)key.GetValue("OverwriteStockLocationText");

                txtAddress.Text = string.IsNullOrEmpty(address) ? "localhost" : address;
                numPort.Value = (port != null) ? (int)port : 6050;

                checkAutoConnect.Checked = string.IsNullOrEmpty(autoconnect) ? false : bool.Parse(autoconnect);

                checkAllowDeliveryInput.Checked = string.IsNullOrEmpty(allowdeliveryinput)
                    ? false
                    : bool.Parse(allowdeliveryinput);
                checkAllowStockReturnInput.Checked = string.IsNullOrEmpty(allowstockretinput)
                    ? false
                    : bool.Parse(allowstockretinput);
                checkOnlyFridgeInput.Checked = string.IsNullOrEmpty(onlyfridgeinput) ? false : bool.Parse(onlyfridgeinput);
                checkOnlyArticlesFromList.Checked = string.IsNullOrEmpty(onlyarticlesfromlist)
                    ? false
                    : bool.Parse(onlyarticlesfromlist);
                checkEnforcePickingIndicator.Checked = string.IsNullOrEmpty(enforcepickingindicator)
                    ? false
                    : bool.Parse(enforcepickingindicator);

                checkEnforceExpiryDateStockReturn.Checked = string.IsNullOrEmpty(enforceexpirydatereturn)
                    ? false
                    : bool.Parse(enforceexpirydatereturn);
                checkEnforceExpiryDateDelivery.Checked = string.IsNullOrEmpty(enforceexpirydatedelivery)
                    ? false
                    : bool.Parse(enforceexpirydatedelivery);
                checkEnforceBatchStockReturn.Checked = string.IsNullOrEmpty(enforcebatchreturn)
                    ? false
                    : bool.Parse(enforcebatchreturn);
                checkEnforceBatchDelivery.Checked = string.IsNullOrEmpty(enforcebatchdelivery)
                    ? false
                    : bool.Parse(enforcebatchdelivery);
                checkEnforceLocationStockReturn.Checked = string.IsNullOrEmpty(enforcelocationreturn)
                    ? false
                    : bool.Parse(enforcelocationreturn);

                checkEnforceLocationNewDelivery.Checked = string.IsNullOrEmpty(enforcelocationnewdelivery)
                    ? false
                    : bool.Parse(enforcelocationnewdelivery);
                checkEnforceSerialNumberStockReturn.Checked = string.IsNullOrEmpty(enforceSerialNumberStockReturn)
                    ? false
                    : bool.Parse(enforceSerialNumberStockReturn);
                checkEnforceSerialNumberNewDelivery.Checked = string.IsNullOrEmpty(enforceSerialNumberNewDelivery)
                    ? false
                    : bool.Parse(enforceSerialNumberNewDelivery);
                checkParseDatamatrixCodes.Checked = string.IsNullOrEmpty(parseDatamatrixCodes)
                    ? false
                    : bool.Parse(parseDatamatrixCodes);

                checkSetMaxSubItemQuantity.Checked = !string.IsNullOrEmpty(setMaxSubItemQuantity)
                    && bool.Parse(setMaxSubItemQuantity);

                rbtnRandomMaxSubItemQuantity.Checked = !string.IsNullOrEmpty(setRandomMaxSubItemQuantity)
                    && bool.Parse(setRandomMaxSubItemQuantity);

                rbtnSpecificMaxSubItemQuantity.Checked = !rbtnRandomMaxSubItemQuantity.Checked;

                try
                {
                    numboxSpecificMaxSubItemQuantity.Value = int.Parse(randomMaxSubItemQuantity);
                }
                catch
                {
                    numboxSpecificMaxSubItemQuantity.Value = numboxSpecificMaxSubItemQuantity.Minimum;
                }

                recentSpecificMaxSubItemQuantity = numboxSpecificMaxSubItemQuantity.Value.ToString();

                checkBoxSetVirtualArticle.Checked = string.IsNullOrEmpty(boxsetvirtualarticle)
                    ? false
                    : bool.Parse(boxsetvirtualarticle);

                checkOverwriteArticleName.Checked = string.IsNullOrEmpty(overwriteArticleName)
                    ? false
                    : bool.Parse(overwriteArticleName);

                checkOverwriteStockLocation.Checked = string.IsNullOrEmpty(overwritelocation)
                    ? false
                    : bool.Parse(overwritelocation);

                txtOverwriteArticleName.Text = overwriteArticleNameText ?? string.Empty;

                txtOverwriteStockLocation.Text = string.IsNullOrEmpty(overwritelocationtext)
                    ? string.Empty
                    : overwritelocationtext;

                var digitalShelfaddress = (string)key.GetValue("DigitalShelfAddress");
                var digitalShelfport = key.GetValue("DigitalShelfPort");

                textBoxDigitalShelfAddress.Text = string.IsNullOrEmpty(digitalShelfaddress) ? "localhost" : digitalShelfaddress;
                numericUpDownDigitalShelfPort.Value = (digitalShelfport != null) ? (int)digitalShelfport : 6052;
            }
        }

        /// <summary>
        /// Handles the DoWork event of the bgConnect control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        private void bgConnect_DoWork(object sender, DoWorkEventArgs e)
        {
            if (!this._isClosing)
            {
                try
                {
                    var doWorkEventArgs = (List<object>)e.Argument;

                    var connectionParameters = doWorkEventArgs.OfType<string[]>().First();

                    var selectedCapabilities = doWorkEventArgs.OfType<List<Capability>>().First();

                    this._storageSystem.Connect(connectionParameters[0], ushort.Parse(connectionParameters[1]), selectedCapabilities);

                    e.Result = true;
                }
                catch (Exception)
                {
                    e.Result = false;
                }
            }
        }

        /// <summary>
        /// Handles the RunWorkerCompleted event of the bgConnect control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        private void bgConnect_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (this._isClosing == false)
            {
                if ((e.Result == null) || ((bool)e.Result == false))
                {
                    if (checkAutoConnect.Checked)
                    {
                        try
                        {
                            ConnectStorageSystem();
                        }
                        catch (Exception ex)
                        {
                            Debugger.Log(10, "error", "Storage connection failed!");
                            //ShowErrorMessage("Storage connection failed",ex);
                        }
                    }
                    else
                    {
                        MessageBox.Show(string.Format("Connecting to storage system '{0}' failed.", txtAddress.Text),
                            "IT System Simulator");
                        btnConnect.Enabled = true;
                    }
                }
                else
                {
                    btnDisconnect.Enabled = true;
                }

                if (checkAutomaticStateObservation.Checked == false)
                {
                    StorageSystem_StateChanged(_storageSystem, ComponentState.Ready);
                    // For testing purpose
                    //StorageSystem_StateChangedExtended(_storageSystem, new StateChangedEventArgs(ComponentState.Ready));
                }
            }
        }

        private static void ShowErrorMessage(string message, Exception ex)
        {
            var mForm = new FRowaMessage();
            mForm.MessageTextBox.Text = $"{message}: {ex.GetType()}  {ex.Message}!";
            mForm.Show();
        }

        /// <summary>
        /// Connects the Storage system. Will not do anything is already connected.
        /// </summary>
        private void ConnectStorageSystem()
        {
            if (this._isClosing == false)
            {
                if (_storageSystem != null)
                {
                    if (_storageSystem.Connected)
                    {
                        // already connected, don't do anything.
                        return;
                    }

                    // Make sure the storage system is Disposed.
                    DisconnectStorageSystem();
                }


                newCheckBoxEntryEnableAllCapabilities.Enabled = false;
                CapabilitiesAvalibleChange(false);

                try
                {
                    if (string.IsNullOrEmpty(txtTenantId.Text))
                    {
                        _storageSystem =
                            new RowaStorageSystem(subscriberID: (int)numboxSubscriberId.Value,
                                stateCheckInterval: Convert.ToInt32(numStateCheckInterval.Value));
                    }
                    else
                    {
                        _storageSystem = new RowaStorageSystem(txtTenantId.Text,
                            subscriberID: (int)numboxSubscriberId.Value,
                            stateCheckInterval: Convert.ToInt32(numStateCheckInterval.Value));
                    }

                    _storageSystem.EnableAutomaticStateObservation = checkAutomaticStateObservation.Checked;
                    _storageSystem.PackDispensed += StorageSystem_PackDispensed;
                    _storageSystem.ArticleInfoRequested += StorageSystem_ArticleInfoRequested;
                    _storageSystem.PackStored += StorageSystem_PackStored;
                    _storageSystem.StateChanged += StorageSystem_StateChanged;
                    _storageSystem.StockUpdated += StorageSystem_StockUpdated;
                    _storageSystem.OutputDestinationButtonPressed += StorageSystem_OutputDestinationButtonPressed;

                    checkEnDisableInputRespons_CheckedChanged(this, EventArgs.Empty);

                    // For dev testing purpose
                    //_storageSystem.PackDispensedExtended += StorageSystem_PackDispensedExtended;
                    //_storageSystem.PackStoredExtended += StorageSystem_PackStoredExtended;
                    //_storageSystem.StateChangedExtended += StorageSystem_StateChangedExtended;
                    //_storageSystem.PackInputRequestedExtended += StorageSystem_PackInputRequestedExtended;
                    //_storageSystem.StockUpdatedExtended += StorageSystem_StockUpdatedExtended;
                    //_storageSystem.ArticleInfoRequestedExtended += StorageSystem_ArticleInfoRequestedExtended;
                }
                finally
                {
                    var connectionParameters = new List<object>
                    {
                        new string[]
                        {
                            txtAddress.Text,
                            numPort.Value.ToString()
                        },
                        this.GetUserSelectedHelloRequestCapabilities()
                    };

                    bgConnect.RunWorkerAsync(connectionParameters);
                }
            }
        }

        /// <summary>
        /// Disconnects and disposes the Storage system instance.
        /// </summary>
        private void DisconnectStorageSystem()
        {
            if (_storageSystem != null)
            {
                _storageSystem.Disconnect();

                CapabilitiesAvalibleChange(true);
                newCheckBoxEntryEnableAllCapabilities.Enabled = true;
                _storageSystem.PackDispensed -= StorageSystem_PackDispensed;
                _storageSystem.PackInputRequested -= StorageSystem_PackInputRequested;
                _storageSystem.ArticleInfoRequested -= StorageSystem_ArticleInfoRequested;
                _storageSystem.PackStored -= StorageSystem_PackStored;
                _storageSystem.StateChanged -= StorageSystem_StateChanged;
                _storageSystem.StockUpdated -= StorageSystem_StockUpdated;
                _storageSystem.OutputDestinationButtonPressed -= StorageSystem_OutputDestinationButtonPressed;

                // For dev testing purpose
                //_storageSystem.PackDispensedExtended -= StorageSystem_PackDispensedExtended;
                //_storageSystem.PackStoredExtended -= StorageSystem_PackStoredExtended;
                //_storageSystem.StateChangedExtended -= StorageSystem_StateChangedExtended;
                //_storageSystem.PackInputRequestedExtended -= StorageSystem_PackInputRequestedExtended;
                //_storageSystem.StockUpdatedExtended -= StorageSystem_StockUpdatedExtended;
                //_storageSystem.ArticleInfoRequestedExtended -= StorageSystem_ArticleInfoRequestedExtended;

                _storageSystem.Dispose();
                _storageSystem = null;
            }
        }

        /// <summary>
        /// Handles the FormClosing event of the FMain control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="FormClosingEventArgs"/> instance containing the event data.</param>
        private void FMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            this._isClosing = true;

            DisconnectStorageSystem();

            _digitalShelf.Disconnect();
            _digitalShelf.ArticleInfoRequested -= DigitalShelf_ArticleInfoRequested;
            _digitalShelf.ArticlePriceRequested -= DigitalShelf_ArticlePriceRequested;
            _digitalShelf.ArticleSelected -= DigitalShelf_ArticleSelected;
            _digitalShelf.ShoppingCartRequested -= DigitalShelf_ShoppingCartRequested;
            _digitalShelf.ShoppingCartUpdateRequested -= DigitalShelf_ShoppingCartUpdateRequested;
            _digitalShelf.Dispose();

            // store as last known working
            SaveConfigurationsToRegistry();
        }

        /// <summary>
        /// Save configuration To Registry
        /// </summary>
        private void SaveConfigurationsToRegistry()
        {
            using (var key = Registry.CurrentUser.CreateSubKey("Software\\CareFusion\\ITSystemSimulator"))
            {
                //Ip stuff
                key.SetValue("StorageSystemAddress", txtAddress.Text);
                key.SetValue("StorageSystemPort", (int)numPort.Value, RegistryValueKind.DWord);
                //Auto Connect
                key.SetValue("AutoConnectEnable", checkAutoConnect.Checked.ToString());
                //Input Tab
                key.SetValue("AllowDeliveryInput", checkAllowDeliveryInput.Checked.ToString());
                key.SetValue("AllowStockReturnInput", checkAllowStockReturnInput.Checked.ToString());
                key.SetValue("OnlyFridgeInput", checkOnlyFridgeInput.Checked.ToString());
                key.SetValue("OnlyArticlesFromList", checkOnlyArticlesFromList.Checked.ToString());
                key.SetValue("EnforcePickingIndicator", checkEnforcePickingIndicator.Checked.ToString());
                key.SetValue("EnforceExpiryDateStockReturn", checkEnforceExpiryDateStockReturn.Checked.ToString());
                key.SetValue("EnforceExpiryDateDelivery", checkEnforceExpiryDateDelivery.Checked.ToString());
                key.SetValue("EnforceBatchStockReturn", checkEnforceBatchStockReturn.Checked.ToString());
                key.SetValue("EnforceBatchDelivery", checkEnforceBatchDelivery.Checked.ToString());
                key.SetValue("EnforceLocationStockReturn", checkEnforceLocationStockReturn.Checked.ToString());
                key.SetValue("EnforceLocationNewDelivery", checkEnforceLocationNewDelivery.Checked.ToString());
                key.SetValue("EnforceSerialNumberStockReturn", checkEnforceSerialNumberStockReturn.Checked.ToString());
                key.SetValue("EnforceSerialNumberNewDelivery", checkEnforceSerialNumberNewDelivery.Checked.ToString());
                key.SetValue("ParseDatamatrixCodes", checkParseDatamatrixCodes.Checked.ToString());
                key.SetValue("SetMaxSubItemQuantity", checkSetMaxSubItemQuantity.Checked.ToString());
                key.SetValue("SetRandomMaxSubItemQuantity", rbtnRandomMaxSubItemQuantity.Checked.ToString());
                key.SetValue("RandomMaxSubItemQuantity", numboxSpecificMaxSubItemQuantity.Value.ToString());
                key.SetValue("BoxSetVirtualArticle", checkBoxSetVirtualArticle.Checked.ToString());
                key.SetValue("OverwriteArticleName", checkOverwriteArticleName.Checked.ToString());
                key.SetValue("OverwriteStockLocation", checkOverwriteStockLocation.Checked.ToString());

                key.SetValue("OverwriteArticleNameText", txtOverwriteArticleName.Text);
                key.SetValue("OverwriteStockLocationText", txtOverwriteStockLocation.Text);
            }
        }

        /// <summary>
        /// Sets the read only options for the specified data grid control.
        /// </summary>
        /// <param name="dataGridView">The data grid view.</param>
        private void SetReadOnlyDataGridOptions(DataGridView dataGridView)
        {
            dataGridView.AllowUserToAddRows = false;
            dataGridView.AllowUserToDeleteRows = false;
            dataGridView.AllowUserToOrderColumns = false;
            dataGridView.AllowUserToResizeColumns = false;
            dataGridView.AllowUserToResizeRows = false;
            dataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.None;
            dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dataGridView.ColumnHeadersVisible = true;
            dataGridView.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            dataGridView.MultiSelect = false;
            dataGridView.ReadOnly = true;
            dataGridView.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dataGridView.RowHeadersVisible = false;
            dataGridView.ShowCellErrors = false;
            dataGridView.ShowCellToolTips = false;
            dataGridView.ShowEditingIcon = false;
            dataGridView.ShowRowErrors = false;
            dataGridView.EnableHeadersVisualStyles = false;
            dataGridView.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
        }

        /// <summary>
        /// Handles the TextChanged event of the txtAddress control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void txtAddress_TextChanged(object sender, EventArgs e)
        {
            btnConnect.Enabled = (string.IsNullOrEmpty(txtAddress.Text) == false);
        }

        /// <summary>
        /// Handles the Click event of the btnConnect control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                btnConnect.Enabled = false;
                ConnectStorageSystem();
            }
            catch (Exception ex)
            {
                btnConnect.Enabled = true;
                ShowErrorMessage("Storage connection failed", ex);
            }
        }

        /// <summary>
        /// Handles the Click event of the btnDisconnect control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            checkAutoConnect.Checked = false; // if manually disconnect, disable auto-connect.
            btnDisconnect.Enabled = false;
            DisconnectStorageSystem();
            btnConnect.Enabled = true;
            btnBulkOrder.Enabled = false;
            btnCloneOrder.Enabled = false;
        }

        /// <summary>
        /// Handles the CheckedChanged event of the checkAutomaticStateObservation control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void checkAutomaticStateObservation_CheckedChanged(object sender, EventArgs e)
        {
            if (_storageSystem != null)
            {
                _storageSystem.EnableAutomaticStateObservation = checkAutomaticStateObservation.Checked;
            }
        }

        /// <summary>
        /// Handles the TextChanged event of the txtArticleFilter control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void txtArticleFilter_TextChanged(object sender, EventArgs e)
        {
            _stockModel.SetFilter(txtArticleFilter.Text);

            this.LoadStockInfoDataGrids();
        }

        /// <summary>
        /// Handles the Click event of the btnReloadStock control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnReloadStock_Click(object sender, EventArgs e)
        {
            var StockInfoCapability = (CheckBox)this.Controls
                    .Find($"chkboxEnable_{nameof(HelloRequest)}_StockInfo", true)
                    .First();
            if (newCheckBoxEntryEnableAllCapabilities.Checked && !StockInfoCapability.Checked) { MessageBox.Show("StockInfo Capability is disabled. Reload Stock will not be triggered!"); return; }

            btnReloadStock.Enabled = false;
            _stockModel.Clear();

            bgStock.RunWorkerAsync(new UserStockOptions
            {
                ArticleFilterName = txtArticleFilter.Text,
                IncludeArticleDetails = checkIncludeArticleDetails.Checked,
                IncludePacks = checkIncludePacks.Checked
            });
        }

        /// <summary>
        /// Handles the DoWork event of the bgStock control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        private void bgStock_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                var userStockOptions = e.Argument as IUserStockOptions;

                if (string.IsNullOrEmpty(userStockOptions.ArticleFilterName))
                {
                    e.Result = _storageSystem.GetStock(userStockOptions.IncludePacks, userStockOptions.IncludeArticleDetails);
                }
                else
                {
                    e.Result = _storageSystem.GetStock(userStockOptions.IncludePacks, userStockOptions.IncludeArticleDetails, userStockOptions.ArticleFilterName);
                }
                
            }
            catch (Exception)
            {
                e.Result = null;
            }
        }

        /// <summary>
        /// Handles the RunWorkerCompleted event of the bgStock control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        /// <remarks>When a user selects not to include pack details on the StockInfoResponse, product's quantity is sent through article details. In this case we update the quantity from article in order to display it on dataGridArticles.</remarks>
        private void bgStock_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            if (e.Result != null)
            {
                var articlesResponse = (List<IArticle>)e.Result;

                _stockModel.Replace(articlesResponse);

                this.LoadStockInfoDataGrids();

                btnBulkOrder.Enabled = _stockModel.HasAnyStock();
            }
            else
            {
                MessageBox.Show("Loading stock failed.", "IT System Simulator");
            }

            if (checkAutomaticStateObservation.Checked)
            {
                btnReloadStock.Enabled = (_storageSystem.State != ComponentState.NotConnected);
            }
            else
            {
                btnReloadStock.Enabled = true;
            }
        }

        /// <summary>
        /// Loads input tab's DataGrids with provided StockInfoResponse info
        /// </summary>
        private void LoadStockInfoDataGrids()
        {
            dataGridPacks_SizeChanged(dataGridPacks, null);
            dataGridArticles_SizeChanged(dataGridArticles, null);
        }

        /// <summary>
        /// Handles the Click event of the btnSendInitInputRequest control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnSendInitInputRequest_Click(object sender, EventArgs e)
        {
            btnSendInitInputRequest.Enabled = false;

            var request = _storageSystem.CreateInitiateInputRequest(numInitInputID.Value.ToString(),
                                                                    (int)numInputSource.Value,
                                                                    (int)numInputPoint.Value,
                                                                    (int)numDestination.Value,
                                                                    string.IsNullOrEmpty(txtDeliveryNumber.Text) ? null : txtDeliveryNumber.Text,
                                                                    chkSetPickingIndicator.Checked);

            if (request == null)
            {
                MessageBox.Show("Input initiation is not supported by the storage system.");
                return;
            }

            request.Finished += OnInitiateInputRequest_Finished;

            lock (_activeInitiatedInputs)
            {
                _activeInitiatedInputs.Add(request);
            }

            DateTime? expiryDate = null;
            DateTime expiryDateCheck;
            if (DateTime.TryParse(txtPackExpiryDate.Text, out expiryDateCheck))
            {
                expiryDate = expiryDateCheck;
            }

            request.AddInputPack(txtPackScanCode.Text,
                                 txtPackBatchNumber.Text,
                                 null,
                                 expiryDate,
                                 (int)numPackSubItemQuantity.Value,
                                 (int)numPackDepth.Value,
                                 (int)numPackWidth.Value,
                                 (int)numPackHeight.Value,
                                 (PackShape)Enum.Parse(typeof(PackShape), cmbPackShape.Text),
                                 string.IsNullOrEmpty(txtPackStockLocation.Text) ? null : txtPackStockLocation.Text);

            bgInitInput.RunWorkerAsync(request);
        }

        /// <summary>
        /// Handles the DoWork event of the bgInitInput control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        private void bgInitInput_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            var request = (IInitiateInputRequest)e.Argument;

            bgInitInput.ReportProgress(1, string.Format("Sending InitiateInputRequest({0}) ...", request.Id));

            try
            {
                request.Start();

                bgInitInput.ReportProgress(1, string.Format("InitiateInputRequest({0}) successfully sent.", request.Id));
                bgInitInput.ReportProgress(1, string.Format("  -> Selected InputSource = {0}", request.InputSource));
                bgInitInput.ReportProgress(1, string.Format("  -> Selected InputPoint = {0}", request.InputPoint));

                foreach (var article in request.InputArticles)
                {
                    bgInitInput.ReportProgress(1, string.Format("  -> Seems to be Article:\n\tID: '{0}'\n\tName: '{1}'\n\tDosageForm: '{2}'\n\tPackagingUnit: '{3}'.",
                                                                article.Id, article.Name, article.DosageForm, article.PackagingUnit));

                }

                e.Result = true;
            }
            catch (Exception ex)
            {
                bgInitInput.ReportProgress(1, string.Format("Sending InitiateInputRequest({0}) failed with error '{1}'.", request.Id, ex.Message));
                e.Result = ex;
            }
        }

        /// <summary>
        /// Handles the ProgressChanged event of the bgInitInput control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.ProgressChangedEventArgs"/> instance containing the event data.</param>
        private void bgInitInput_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            if (e.UserState != null)
                WriteInitiateInputLog((string)e.UserState);
        }

        /// <summary>
        /// Handles the RunWorkerCompleted event of the bgInitInput control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        private void bgInitInput_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            if ((e.Result != null) && (e.Result is Exception))
            {
                MessageBox.Show("Sending Input Initiation failed.", "IT System Simulator");
            }

            btnSendInitInputRequest.Enabled = btnNewOrder.Enabled;
        }

        /// <summary>
        /// Handles the SizeChanged event of the dataGridArticles control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridArticles_SizeChanged(object sender, EventArgs e)
        {
            if (dataGridArticles.Columns.Count >= 6)
            {
                dataGridArticles.Columns[0].Width = (dataGridArticles.Width - 30) / 7;
                dataGridArticles.Columns[1].Width = dataGridArticles.Columns[0].Width;
                dataGridArticles.Columns[2].Width = dataGridArticles.Columns[0].Width;
                dataGridArticles.Columns[3].Width = dataGridArticles.Columns[0].Width;
                dataGridArticles.Columns[4].Width = dataGridArticles.Columns[0].Width;
                dataGridArticles.Columns[5].Width = dataGridArticles.Columns[0].Width;
                dataGridArticles.Columns[6].Width = dataGridArticles.Columns[0].Width;
            }
        }

        /// <summary>
        /// Handles the SizeChanged event of the dataGridPacks control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridPacks_SizeChanged(object sender, EventArgs e)
        {
            if (dataGridPacks.Columns.Count >= 15)
            {
                dataGridPacks.Columns[0].Width = (dataGridPacks.Width - 30) / 15;

                for (int i = 1; i < 15; ++i)
                {
                    dataGridPacks.Columns[i].Width = dataGridPacks.Columns[0].Width;
                }
            }
        }

        /// <summary>
        /// Handles the SizeChanged event of the dataGridOrders control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridOrders_SizeChanged(object sender, EventArgs e)
        {
            if (dataGridOrders.Columns.Count >= 6)
            {
                dataGridOrders.Columns[0].Width = (dataGridOrders.Width - 30) / 6;

                for (int i = 1; i < 6; ++i)
                {
                    dataGridOrders.Columns[i].Width = dataGridOrders.Columns[0].Width;
                }
            }
        }

        /// <summary>
        /// Handles the SizeChanged event of the dataGridOrderItems control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridOrderItems_SizeChanged(object sender, EventArgs e)
        {
            if (dataGridOrderItems.Columns.Count >= 9)
            {
                dataGridOrderItems.Columns[0].Width = (dataGridOrderItems.Width - 30) / 9;

                for (int i = 1; i < 9; ++i)
                {
                    dataGridOrderItems.Columns[i].Width = dataGridOrderItems.Columns[0].Width;
                }
            }
        }

        /// <summary>
        /// Handles the SizeChanged event of the dataGridItemLabels control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridItemLabels_SizeChanged(object sender, EventArgs e)
        {
            if (dataGridItemLabels.Columns.Count >= 2)
            {
                dataGridItemLabels.Columns[0].Width = 100;
                dataGridItemLabels.Columns[1].Width = dataGridItemLabels.Width - 130;
            }
        }

        /// <summary>
        /// Handles the SizeChanged event of the dataGridDispensedPacks control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridDispensedPacks_SizeChanged(object sender, EventArgs e)
        {
            if (dataGridDispensedPacks.Columns.Count >= 11)
            {
                dataGridDispensedPacks.Columns[0].Width = (dataGridDispensedPacks.Width - 30) / 11;

                for (int i = 1; i < 11; ++i)
                {
                    dataGridDispensedPacks.Columns[i].Width = dataGridDispensedPacks.Columns[0].Width;
                }
            }
        }

        /// <summary>
        /// Handles the SizeChanged event of the dataGridResponses control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridResponses_SizeChanged(object sender, EventArgs e)
        {
            if (dataGridResponses.Columns.Count >= 4)
            {
                dataGridResponses.Columns[0].Width = (dataGridResponses.Width - 30) / 4;

                for (int i = 1; i < 4; ++i)
                {
                    dataGridResponses.Columns[i].Width = dataGridResponses.Columns[0].Width;
                }
            }
        }

        /// <summary>
        /// Handles the SizeChanged event of the dataGridButtonsPressed control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridButtonsPressed_SizeChanged(object sender, EventArgs e)
        {
            if (dataGridButtonsPressed.Columns.Count >= 2)
            {
                dataGridButtonsPressed.Columns[0].Width = 100;
                dataGridButtonsPressed.Columns[1].Width = dataGridItemLabels.Width - 130;
            }
        }

        /// <summary>
        /// Handles the SelectionChanged event of the dataGridArticles control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridArticles_SelectionChanged(object sender, EventArgs e)
        {
            if ((dataGridArticles.SelectedRows == null) || (dataGridArticles.SelectedRows.Count == 0))
            {
                _stockModel.ClearArticleSelection();
            }
            else
            {
                DataRowView rowView = dataGridArticles.SelectedRows[0].DataBoundItem as DataRowView;
                _stockModel.SelectArticle(rowView.Row[0] as string);
            }
        }

        /// <summary>
        /// Handles the Click event of the btnClearInputLog control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnClearInputLog_Click(object sender, EventArgs e)
        {
            listInputLog.Items.Clear();
        }

        /// <summary>
        /// Handles the Click event of the btnNewOrder control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnNewOrder_Click(object sender, EventArgs e)
        {
            FOrder newOrder = new FOrder(_storageSystem, _stockModel.ArticlePacks.Keys.ToArray());
            newOrder.ShowDialog(this);

            if (newOrder.Order != null)
            {
                _orderModel.AddOrder(newOrder.Order);
                btnCloneOrder.Enabled = true;
            }
        }

        /// <summary>
        /// Handles the Click event of the btnBulkOrder control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnBulkOrder_Click(object sender, EventArgs e)
        {
            FBulkOrder newBulkOrder = new FBulkOrder(_storageSystem, _stockModel.ArticlePacks);

            if (newBulkOrder.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
            {
                dataGridOrders.DataSource = null;

                foreach (var order in newBulkOrder.Orders)
                {
                    _orderModel.AddOrder(order);
                }

                dataGridOrders.DataSource = _orderModel.Orders;
                dataGridOrders_SizeChanged(dataGridOrders, new EventArgs());
            }
        }

        /// <summary>
        /// Handles the Click event of the btnSendOrder control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnSendOrder_Click(object sender, EventArgs e)
        {
            btnSendOrder.Enabled = false;
            _orderModel.SendOrders();
            btnSendOrder.Enabled = true;
        }

        /// <summary>
        /// Handles the Click event of the btnCancelOrder control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnCancelOrder_Click(object sender, EventArgs e)
        {
            btnCancelOrder.Enabled = false;
            _orderModel.CancelCurrentOrder();
            btnCancelOrder.Enabled = true;
        }

        /// <summary>
        /// Handles the Click event of the btnClearOrders control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnClearOrders_Click(object sender, EventArgs e)
        {
            _orderModel.Clear();
            btnCloneOrder.Enabled = false;
        }

        /// <summary>
        /// Handles the SelectionChanged event of the dataGridOrders control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridOrders_SelectionChanged(object sender, EventArgs e)
        {
            if ((dataGridOrders.SelectedRows == null) || (dataGridOrders.SelectedRows.Count == 0))
            {
                _orderModel.ClearOrderSelection();
            }
            else
            {
                DataRowView rowView = dataGridOrders.SelectedRows[0].DataBoundItem as DataRowView;
                _orderModel.SelectOrder((string)rowView.Row[0]);
            }
        }

        /// <summary>
        /// Handles the SelectionChanged event of the dataGridOrderItems control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridOrderItems_SelectionChanged(object sender, EventArgs e)
        {
            if ((dataGridOrderItems.SelectedRows == null) || (dataGridOrderItems.SelectedRows.Count == 0))
            {
                _orderModel.ClearOrderItemSelection();
            }
            else
            {
                DataRowView rowView = dataGridOrderItems.SelectedRows[0].DataBoundItem as DataRowView;
                _orderModel.SelectOrderItem((int)rowView.Row[0]);
            }
        }

        /// <summary>
        /// Handles the SizeChanged event of the dataGridDeliveyItems control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridDeliveryItems_SizeChanged(object sender, EventArgs e)
        {
            if (dataGridDeliveryItems.Columns.Count >= 13)
            {
                dataGridDeliveryItems.Columns[0].Width = (dataGridDeliveryItems.Width - 70) / 13;

                for (int i = 1; i < 13; ++i)
                {
                    dataGridDeliveryItems.Columns[i].Width = dataGridDeliveryItems.Columns[0].Width;
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the btnSendStockDeliveries control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnSendStockDeliveries_Click(object sender, EventArgs e)
        {
            try
            {
                btnSendStockDeliveries.Enabled = false;
                _stockDeliveryModel.Send(_storageSystem);
            }
            catch (Exception ex)
            {
                var msg = string.Format("Adding stock deliveries failed!\n\n{0}", ex.Message);
                MessageBox.Show(msg, "IT System Simulator");
            }

            btnSendStockDeliveries.Enabled = true;
        }

        /// <summary>
        /// Handles the Click event of the btnGetConfig control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnGetConfig_Click(object sender, EventArgs e)
        {
            txtConfiguration.Text = string.Empty;

            if ((_storageSystem != null) && (_storageSystem.Connected))
            {
                txtConfiguration.Text = _storageSystem.Configuration.Replace("><", ">\r\n<");
            }
        }

        /// <summary>
        /// Handles the SizeChanged event of the dataGridArticleInfo control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridArticleInfo_SizeChanged(object sender, EventArgs e)
        {
            if (dataGridArticleInfo.Columns.Count >= 2)
            {
                dataGridArticleInfo.Columns[0].Width = (dataGridArticleInfo.Width - 80);
                dataGridArticleInfo.Columns[1].Width = 50;
            }
        }

        /// <summary>
        /// Handles the SelectionChanged event of the dataGridArticleInfo control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridArticleInfo_SelectionChanged(object sender, EventArgs e)
        {
            if ((dataGridArticleInfo.SelectedRows == null) || (dataGridArticleInfo.SelectedRows.Count == 0))
            {
                _taskModel.ClearArticleSelection();
            }
            else
            {
                DataRowView rowView = dataGridArticleInfo.SelectedRows[0].DataBoundItem as DataRowView;
                _taskModel.SelectArticle(rowView.Row[0] as string);
            }
        }

        /// <summary>
        /// Handles the SizeChanged event of the dataGridPackInfo control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridPackInfo_SizeChanged(object sender, EventArgs e)
        {
            if (dataGridPackInfo.Columns.Count >= 14)
            {
                dataGridPackInfo.Columns[0].Width = (dataGridPackInfo.Width - 30) / 14;

                for (int i = 1; i < 14; ++i)
                {
                    dataGridPackInfo.Columns[i].Width = dataGridPackInfo.Columns[0].Width;
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the btnGetTaskInfo control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnGetTaskInfo_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtTaskID.Text))
                return;

            if (cmbTaskType.SelectedIndex == 0)
            {
                var info = _storageSystem.GetOutputProcessInfo(txtTaskID.Text);
                lblTaskState.Text = info.State.ToString();
                _taskModel.Update(info.Packs);
            }
            else
            {
                var info = _storageSystem.GetStockDeliveryInfo(txtTaskID.Text);
                lblTaskState.Text = info.State.ToString();
                _taskModel.Update(info.InputArticles);
            }
        }

        /// <summary>
        /// Handles the Click event of the btnClearInitInputLog control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnClearInitInputLog_Click(object sender, EventArgs e)
        {
            listInitInputLog.Items.Clear();
        }

        /// <summary>
        /// Handles the SizeChanged event of the dataGridComponents control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridComponents_SizeChanged(object sender, EventArgs e)
        {
            if (dataGridComponents.Columns.Count >= 4)
            {
                dataGridComponents.Columns[0].Width = (dataGridComponents.Width - 30) / 4;

                for (int i = 1; i < 4; ++i)
                {
                    dataGridComponents.Columns[i].Width = dataGridComponents.Columns[0].Width;
                }
            }
        }

        /// <summary>
        /// Handles the SizeChanged event of the dataGridStockLocations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void dataGridStockLocations_SizeChanged(object sender, EventArgs e)
        {
            if (dataGridStockLocations.Columns.Count >= 2)
            {
                dataGridStockLocations.Columns[0].Width = (dataGridStockLocations.Width - 30) / 2;

                for (int i = 1; i < 2; ++i)
                {
                    dataGridStockLocations.Columns[i].Width = dataGridStockLocations.Columns[0].Width;
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the btnReloadComponents control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnReloadComponents_Click(object sender, EventArgs e)
        {
            btnReloadComponents.Enabled = false;
            _componentsModel.Update(_storageSystem.ComponentStates);
            btnReloadComponents.Enabled = true;
        }

        /// <summary>
        /// Handles the Click event of the btnReloadStockLocations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnReloadStockLocations_Click(object sender, EventArgs e)
        {
            btnReloadStockLocations.Enabled = false;
            _stockLocationModel.Update(_storageSystem.StockLocations);
            btnReloadStockLocations.Enabled = true;
        }

        /// <summary>
        /// Handles the CheckedChanged event of the checkSetMaxSubItemQuantity control.
        /// When not checked, radio buttons for setting random or specific maxSubItemQuantity are disabled
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void checkSetMaxSubItemQuantity_CheckedChanged(object sender, EventArgs e)
        {
            pnlSetMaxSubItemQuantity.Enabled = checkSetMaxSubItemQuantity.Checked;

            this.rbtnEnableSpecificMaxSubItemQuantity();
        }

        /// <summary>
        /// Handles the CheckedChanged event of the rbtnSpecificMaxSubItemQuantity control.
        /// When not checked, numberBox for setting specific maxSubItemQuantity is disabled
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void rbtnSpecificMaxSubItemQuantity_CheckedChanged(object sender, EventArgs e)
        {
            this.rbtnEnableSpecificMaxSubItemQuantity();
        }

        /// <summary>
        /// Decides whether the numberBox for setting specific maxSubItemQuantity should be disabled or not.
        /// In order to be enabled both checkSetMaxSubItemQuantity and rbtnSpecificMaxSubItemQuantity must be checked
        /// </summary>
        private void rbtnEnableSpecificMaxSubItemQuantity()
        {
            numboxSpecificMaxSubItemQuantity.Enabled = checkSetMaxSubItemQuantity.Checked && rbtnSpecificMaxSubItemQuantity.Checked;
        }

        /// <summary>
        /// Handles the CheckedChanged event of the checkOverwriteArticleName control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void checkOverwriteArticleName_CheckedChanged(object sender, EventArgs e)
        {
            txtOverwriteArticleName.Enabled = checkOverwriteArticleName.Checked;
        }

        /// <summary>
        /// Handles the CheckedChanged event of the checkOverwriteStockLocation control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void checkOverwriteStockLocation_CheckedChanged(object sender, EventArgs e)
        {
            txtOverwriteStockLocation.Enabled = checkOverwriteStockLocation.Checked;
        }

        /// <summary>
        /// Event that the specified initiated input process finished.
        /// </summary>
        /// <param name="sender">Sender which raised the event.</param>
        /// <param name="e">Always null.</param>
        private void OnInitiateInputRequest_Finished(object sender, EventArgs e)
        {
            var request = (IInitiateInputRequest)sender;

            WriteInitiateInputLog("InitiateInputRequest({0}) finshed with result '{1}'!", request.Id, request.State.ToString());

            foreach (var article in request.InputArticles)
            {
                WriteInitiateInputLog("  -> Processed Article:\n\tID: '{0}'\n\tName: '{1}'\n\tDosageForm: '{2}'\n\tPackagingUnit: '{3}'.",
                                      article.Id, article.Name, article.DosageForm, article.PackagingUnit);

                foreach (var pack in article.Packs)
                {
                    InputErrorType errorType;
                    string errorText;

                    if (request.GetProcessedPackError(pack, out errorType, out errorText))
                    {
                        WriteInitiateInputLog("  -> Got pack error '{0}' / '{1}'.", errorType.ToString(), errorText);
                    }
                    else
                    {
                        WriteInitiateInputLog("  -> Stored Pack with ID '{0}'.", pack.Id);
                    }
                }
            }

            lock (_activeInitiatedInputs)
            {
                _activeInitiatedInputs.Remove(request);
            }
        }

        /// <summary>
        /// Handles the state change of a storage system.
        /// </summary>
        /// <param name="sender">Object instance which raised the event.</param>
        /// <param name="state">New state of the storage system.</param>
        private void StorageSystem_StateChanged(IStorageSystem sender, ComponentState state)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new StateChangedEventHandler(StorageSystem_StateChanged), sender, state);
                return;
            }

            lblStorageSystemStatus.Text = string.Format("Robot Status: {0}", state.ToString());
            btnReloadStock.Enabled = ((bgStock.IsBusy == false) && (state != ComponentState.NotConnected));

            var newStatus = (state != ComponentState.NotConnected);
            btnNewOrder.Enabled = newStatus;
            btnSendOrder.Enabled = newStatus;
            btnCancelOrder.Enabled = newStatus;
            btnSendMasterArticles.Enabled = newStatus;
            btnSendStockDeliveries.Enabled = newStatus;
            btnSendInitInputRequest.Enabled = newStatus;
            btnSendInfeedInputRequest.Enabled = newStatus;
            btnGetTaskInfo.Enabled = newStatus;
            btnReloadComponents.Enabled = newStatus;
            btnReloadStockLocations.Enabled = newStatus;

            if (newStatus)
            {
                btnBulkOrder.Enabled = _stockModel.HasAnyStock();
            }
            else
            {
                btnBulkOrder.Enabled = false;
            }

            if (checkAutoConnect.Checked)
            {
                btnConnect.Enabled = false;
                if (state == ComponentState.NotConnected)
                {
                    ConnectStorageSystem();
                }
            }
            else
            {
                btnConnect.Enabled = (state == ComponentState.NotConnected);
            }
            btnDisconnect.Enabled = (state != ComponentState.NotConnected);
        }

        /// <summary>
        /// Handles the state change of a storage system.
        /// </summary>
        /// <param name="sender">Object instance which raised the event.</param>
        /// <param name="eventArgs">The event arguments.</param>
        private void StorageSystem_StateChangedExtended(object sender, StateChangedEventArgs eventArgs)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => this.StorageSystem_StateChangedExtended(sender, eventArgs)));
                return;
            }

            lblStorageSystemStatus.Text = string.Format("Robot Status: {0}", eventArgs.State.ToString());
            btnReloadStock.Enabled = ((bgStock.IsBusy == false) && (eventArgs.State != ComponentState.NotConnected));

            btnNewOrder.Enabled = (eventArgs.State != ComponentState.NotConnected);
            btnSendOrder.Enabled = btnNewOrder.Enabled;
            btnCancelOrder.Enabled = btnNewOrder.Enabled;
            btnSendMasterArticles.Enabled = btnNewOrder.Enabled;
            btnSendStockDeliveries.Enabled = btnNewOrder.Enabled;
            btnSendInitInputRequest.Enabled = btnNewOrder.Enabled;
            btnSendInfeedInputRequest.Enabled = btnNewOrder.Enabled;
            btnGetTaskInfo.Enabled = btnNewOrder.Enabled;
            btnReloadComponents.Enabled = btnNewOrder.Enabled;
            btnReloadStockLocations.Enabled = btnNewOrder.Enabled;

            if (btnNewOrder.Enabled)
            {
                btnBulkOrder.Enabled = _stockModel.HasAnyStock();
            }
            else
            {
                btnBulkOrder.Enabled = false;
            }

            if (checkAutoConnect.Checked)
            {
                btnConnect.Enabled = false;
                if (eventArgs.State == ComponentState.NotConnected)
                {
                    ConnectStorageSystem();
                }
            }
            else
            {
                btnConnect.Enabled = (eventArgs.State == ComponentState.NotConnected);
            }
            btnDisconnect.Enabled = (eventArgs.State != ComponentState.NotConnected);
        }

        /// <summary>m
        /// Handles the event that parts of the stock have been updated.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="articleList">The articles and packs that have been updated.</param>
        private void StorageSystem_StockUpdated(IStorageSystem sender, IArticle[] articleList)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new StockUpdatedEventHandler(StorageSystem_StockUpdated), sender, articleList);
                return;
            }

            _stockModel.Update(articleList, true);

            this.LoadStockInfoDataGrids();
        }

        /// <summary>
        /// Event which is raised whenever a connected storage system requests permission for pack input. The specified
        /// request object is used to get further details  and to allow or deny the pack input.
        /// </summary>
        /// <param name="sender">Object instance which raised the event.</param>
        /// <param name="request">Object which contains the details about the requested pack input.</param>
        private void StorageSystem_PackInputRequested(IStorageSystem sender, IInputRequest request)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new PackInputRequestEventHandler(StorageSystem_PackInputRequested), sender, request);

                return;
            }

            if (string.IsNullOrEmpty(request.DeliveryNumber))
            {
                this.HandleStockReturnInput(request);
            }
            else
            {
                this.HandleStockDeliveryInput(request);
            }

            request.Finish();
        }

        /// <summary>
        /// Event which is raised whenever a connected storage system requests
        /// permission for pack input. The specified request object is used to get further details 
        /// and to allow or deny the pack input.
        /// </summary>
        /// <param name="sender">The object instance which raised the event.</param>
        /// <param name="eventArgs">The event arguments.</param>
        private void StorageSystem_PackInputRequestedExtended(object sender, PackInputRequestedEventArgs eventArgs)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => this.StorageSystem_PackInputRequestedExtended(sender, eventArgs)));

                return;
            }

            var inputRequestClone = this.Clone((InputRequest)eventArgs.Request);

            if (eventArgs.Request.DeliveryNumber == null)
            {
                HandleStockReturnInput(eventArgs.Request);
            }
            else
            {
                HandleStockDeliveryInput(eventArgs.Request);
            }

            eventArgs.Request.Finish();
        }

        /// <summary>
        /// Handles the specified stock return input.
        /// </summary>
        /// <param name="request">The input request to handle.</param>
        private void HandleStockReturnInput(IInputRequest request)
        {
            WriteInputLog("Received input request for stock return.");

            HandleStockInput(request, true, false);
        }

        /// <summary>
        /// Handles the specified stock delivery input.
        /// </summary>
        /// <param name="request">The input request to handle.</param>
        private void HandleStockDeliveryInput(IInputRequest request)
        {
            WriteInputLog("Received input request for delivery '{0}'.", request.DeliveryNumber);

            HandleStockInput(request, false, true);
        }

        private void HandleStockInput(IInputRequest request, bool isStockReturn, bool isStockDelivery)
        {
            List<UserInputResponsePropertyOptions> inputResponseCoreOptions = GetInputResponseCoreOptions();
            List<UserInputResponsePropertyOptions> inputResponseArticleOptions = GetInputResponseArticleOptions();
            List<UserInputResponsePropertyOptions> inputResponsePackOptions = GetInputResponsePackOptions();
            List<UserInputResponsePropertyOptions> inputResponsePackHandlingOptions = GetInputResponsePackHandlingOptions();

            if (!checkAllowStockReturnInput.Checked)
            {
                WriteInputLog("Rejecting all packs.");

                foreach (var pack in request.Packs)
                {
                    HandleStockInputPack(pack,
                                         InputHandling.Rejected,
                                         default,
                                         null,
                                         inputResponsePackOptions,
                                         inputResponsePackHandlingOptions);
                }

                if (inputResponseCoreOptions != null)
                {
                    this.ModifyInputResponseObject((InputRequest)request, default, inputResponseCoreOptions);
                }

                if (isStockDelivery)
                {
                    request.Finish();
                }

                return;
            }

            if (checkEnforcePickingIndicator.Checked && !request.PickingIndicator)
            {
                WriteInputLog("Rejecting all packs because of missing picking indicator.");

                // reject all packs due to missing picking indicator
                foreach (var pack in request.Packs)
                {
                    var article = _inputArticles.GetArticleByScanCodeOrDefault(pack.ScanCode, this.GetUserInputOptions());

                    if (isStockReturn)
                    {
                        pack.SetArticleInformation(article.Id,
                            article.Name,
                            article.DosageForm,
                            article.PackagingUnit,
                            checkSetMaxSubItemQuantity.Checked ? article.MaxSubItemQuantity : 0,
                            null,
                            null,
                            checkOnlyFridgeInput.Checked || article.RequiresFridge);
                    }

                    if (isStockDelivery)
                    {
                        SetArticleInformation(pack, article);
                    }

                    HandleStockInputPack(pack,
                                         InputHandling.RejectedNoPickingIndicator,
                                         default,
                                         inputResponseArticleOptions,
                                         inputResponsePackOptions,
                                         inputResponsePackHandlingOptions);
                }

                if (inputResponseCoreOptions != null)
                {
                    this.ModifyInputResponseObject((InputRequest)request, default, inputResponseCoreOptions);
                }

                if (isStockDelivery)
                {
                    request.Finish();
                }

                return;
            }

            if (checkOnlyArticlesFromList.Checked)
            {
                var rejectPacks = false;

                // check whether at least one unknown article is in the list
                foreach (var pack in request.Packs)
                {
                    if (_inputArticles.GetArticleByScanCode(pack.ScanCode, this.GetUserInputOptions()) == null)
                    {
                        rejectPacks = true;

                        break;
                    }
                }

                if (rejectPacks)
                {
                    WriteInputLog("Rejecting all packs because at least one article is not in the list.");

                    // reject all packs due to missing picking indicator
                    foreach (var pack in request.Packs)
                    {
                        var article = _inputArticles.GetArticleByScanCodeOrDefault(pack.ScanCode, this.GetUserInputOptions());

                        if (isStockReturn)
                        {
                            pack.SetArticleInformation(article.Id,
                                article.Name,
                                article.DosageForm,
                                article.PackagingUnit,
                                checkSetMaxSubItemQuantity.Checked ? article.MaxSubItemQuantity : 0,
                                null,
                                null,
                                checkOnlyFridgeInput.Checked || article.RequiresFridge);
                        }

                        if (isStockDelivery)
                        {
                            SetArticleInformation(pack, article);
                        }

                        HandleStockInputPack(pack,
                                             InputHandling.Rejected,
                                             "Unknown article.",
                                             inputResponseArticleOptions,
                                             inputResponsePackOptions,
                                             inputResponsePackHandlingOptions);
                    }

                    if (inputResponseCoreOptions != null)
                    {
                        this.ModifyInputResponseObject((InputRequest)request, default, inputResponseCoreOptions);
                    }

                    return;
                }
            }

            foreach (var pack in request.Packs)
            {
                var article = _inputArticles.GetArticleByScanCodeOrDefault(pack.ScanCode, this.GetUserInputOptions());

                if (checkParseDatamatrixCodes.Checked)
                {
                    ParseDatamatrixCode(pack, article);
                }

                try
                {
                    SetArticleInformation(pack, article);
                }
                catch (Exception ex)
                {
                    if (isStockReturn)
                    {
                        HandleStockInputPack(pack,
                                             InputHandling.Rejected,
                                             ex.Message,
                                             inputResponseArticleOptions,
                                             inputResponsePackOptions,
                                             inputResponsePackHandlingOptions);

                        continue;
                    }

                    throw ex;
                }

                if (checkEnforceExpiryDateStockReturn.Checked && !pack.ExpiryDate.HasValue)
                {
                    WriteInputLog("Rejecting pack '{0}' because of missing expiry date.", pack.ScanCode);
                    HandleStockInputPack(pack,
                                         InputHandling.RejectedNoExpiryDate,
                                         default,
                                         inputResponseArticleOptions,
                                         inputResponsePackOptions,
                                         inputResponsePackHandlingOptions);
                    continue;
                }

                if (checkEnforceBatchStockReturn.Checked && string.IsNullOrEmpty(pack.BatchNumber))
                {
                    WriteInputLog("Rejecting pack '{0}' because of missing batch number.", pack.ScanCode);
                    HandleStockInputPack(pack,
                                         InputHandling.RejectedNoBatchNumber,
                                         default,
                                         inputResponseArticleOptions,
                                         inputResponsePackOptions,
                                         inputResponsePackHandlingOptions);
                    continue;
                }

                if (checkEnforceLocationStockReturn.Checked && string.IsNullOrEmpty(pack.StockLocationId))
                {
                    WriteInputLog("Rejecting pack '{0}' because of missing stock location.", pack.ScanCode);
                    HandleStockInputPack(pack,
                                         InputHandling.RejectedNoStockLocation,
                                         default,
                                         inputResponseArticleOptions,
                                         inputResponsePackOptions,
                                         inputResponsePackHandlingOptions);
                    continue;
                }

                if (checkEnforceSerialNumberStockReturn.Checked && string.IsNullOrEmpty(pack.SerialNumber))
                {
                    WriteInputLog("Rejecting pack '{0}' because of missing serial number.", pack.SerialNumber);
                    HandleStockInputPack(pack,
                                         InputHandling.RejectedNoSerialNumber,
                                         default,
                                         inputResponseArticleOptions,
                                         inputResponsePackOptions,
                                         inputResponsePackHandlingOptions);
                    continue;
                }

                var expiryDate = pack.ExpiryDate ?? DateTime.Now.AddMonths((int)numExpiryDateMonth.Value);
                var stockLocation = checkOverwriteStockLocation.Checked ? txtOverwriteStockLocation.Text : null;
                var hasExpireDateSource = Enum.TryParse<ExpiryDateSource>(pack.ExpiryDateSource, true, out var expiryDateSource);
                

                pack.SetPackInformation(string.IsNullOrEmpty(pack.BatchNumber) ? string.Format("BATCH-{0}", pack.ScanCode) : pack.BatchNumber,
                        string.Format("EXTID-{0}", pack.ScanCode),
                        expiryDate,
                        null,
                        stockLocation,
                        null,
                        hasExpireDateSource ? expiryDateSource : null);

                WriteInputLog("Accepting pack '{0}'.", pack.ScanCode);

                var inputHandling = checkOnlyFridgeInput.Checked || article.RequiresFridge ? InputHandling.AllowedForFridge : InputHandling.Allowed;

                HandleStockInputPack(pack,
                                     inputHandling,
                                     default,
                                     inputResponseArticleOptions,
                                     inputResponsePackOptions,
                                     inputResponsePackHandlingOptions);
            }

            if (inputResponseCoreOptions != null)
            {
                this.ModifyInputResponseObject((InputRequest)request, default,
                    inputResponseCoreOptions);
            }
        }

        private void HandleStockInputPack(IInputPack pack, InputHandling inputHandling, string inputHandlingMessage, List<UserInputResponsePropertyOptions> inputResponseArticleOptions, List<UserInputResponsePropertyOptions> inputResponsePackOptions,
            List<UserInputResponsePropertyOptions> inputResponsePackHandlingOptions)
        {
            Debug.Assert(pack != null);

            var packCopy = this.Clone((Pack)pack);

            if (string.IsNullOrEmpty(inputHandlingMessage))
            {
                pack.SetHandling(inputHandling);
            }
            else
            {
                pack.SetHandling(inputHandling, inputHandlingMessage);
            }
           
            if (inputResponseArticleOptions != null)
            {
                this.ModifyInputResponseObject(((Pack)pack).Article, ((Pack)pack).Article,
                    inputResponseArticleOptions);
            }

            if (inputResponsePackOptions != null)
            {
                this.ModifyInputResponseObject((Pack)pack, packCopy,
                    inputResponsePackOptions);
            }

            if (inputResponsePackHandlingOptions != null)
            {
                this.ModifyInputResponseObject(((Pack)pack).Handling, packCopy.Handling,
                    inputResponsePackHandlingOptions);
            }
        }

        /// <summary>
        /// Set Pack information and Virtual pack information
        /// </summary>
        /// <param name="pack">The pack to receive information</param>
        /// <param name="article">Source Article information</param>
        private void SetArticleInformation(IInputPack pack, InputArticle article)
        {
            pack.SetArticleInformation(article.Id,
                article.Name,
                article.DosageForm,
                article.PackagingUnit,
                checkSetMaxSubItemQuantity.Checked ? article.MaxSubItemQuantity : 0,
                checkBoxSetVirtualArticle.Checked ? "Virtual-" + article.Id.Substring(0, article.Id.Length - 1) : null,
                checkBoxSetVirtualArticle.Checked ? "Virtual-" + article.Name : null,
                checkOnlyFridgeInput.Checked || article.RequiresFridge);
        }

        /// <summary>
        /// Event which is raised when detailed information for one or more articles is requested.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="articleList">List of articles request.</param>
        private void StorageSystem_ArticleInfoRequested(IStorageSystem sender, IStorageSystemArticleInfoRequest request)
        {
            foreach (var article in request.ArticleList)
            {
                WriteInputLog("Article information requested for ID {0}, , Depth '{1}', Width '{2}', Height '{3}', Weight '{4}'.",
                    article.Id, article.Depth, article.Width, article.Height, article.Weight);

                var articleInfo = _inputArticles.GetArticleByScanCodeOrDefault(article.Id, this.GetUserInputOptions());
                List<string> productCodeList = new List<string>();
                productCodeList.Add(articleInfo.Id);
                productCodeList.Add(articleInfo.Id + "-1");
                productCodeList.Add(articleInfo.Id + "-2");

                article.SetArticleInformation(articleInfo.Id,
                    articleInfo.Name,
                    articleInfo.DosageForm,
                    articleInfo.PackagingUnit,
                    checkSetMaxSubItemQuantity.Checked ? articleInfo.MaxSubItemQuantity : 0,
                    checkBoxSetVirtualArticle.Checked ? "Virtual-" + articleInfo.Id.Substring(0, articleInfo.Id.Length - 1) : null,
                    checkBoxSetVirtualArticle.Checked ? "Virtual-" + articleInfo.Name : null,
                    checkOnlyFridgeInput.Checked || articleInfo.RequiresFridge,
                    DateTime.Now,
                    productCodeList);
            }

            request.Finish();
        }

        /// <summary>
        /// Event which is raised when detailed information for one or more articles is requested.
        /// </summary>
        /// <param name="sender">The object instance which raised the event.</param>
        /// <param name="eventArgs">The event arguments.</param>
        private void StorageSystem_ArticleInfoRequestedExtended(object sender, ArticleInfoRequestedEventArgs eventArgs)
        {
            foreach (var article in eventArgs.ArticleInfoRequest.ArticleList)
            {
                WriteInputLog("Article information requested for ID {0}, , Depth '{1}', Width '{2}', Height '{3}', Weight '{4}'.",
                    article.Id, article.Depth, article.Width, article.Height, article.Weight);

                var articleInfo = _inputArticles.GetArticleByScanCodeOrDefault(article.Id, this.GetUserInputOptions());

                List<string> productCodeList = new List<string>
                {
                    articleInfo.Id,
                    articleInfo.Id + "-1",
                    articleInfo.Id + "-2"
                };

                article.SetArticleInformation(articleInfo.Id,
                    articleInfo.Name,
                    articleInfo.DosageForm,
                    articleInfo.PackagingUnit,
                    checkSetMaxSubItemQuantity.Checked ? articleInfo.MaxSubItemQuantity : 0,
                    checkBoxSetVirtualArticle.Checked ? "Virtual-" + articleInfo.Id.Substring(0, articleInfo.Id.Length - 1) : null,
                    checkBoxSetVirtualArticle.Checked ? "Virtual-" + articleInfo.Name : null,
                    checkOnlyFridgeInput.Checked || articleInfo.RequiresFridge,
                    DateTime.Now,
                    productCodeList);
            }

            eventArgs.ArticleInfoRequest.Finish();
        }

        /// <summary>
        /// Event which is raised whenever a new pack was successfully stored in a storage system. 
        /// </summary>
        /// <param name="sender">Object instance which raised the event.</param>
        /// <param name="articleList">List of articles with the packs that were stored.</param>
        private void StorageSystem_PackStored(IStorageSystem sender, IArticle[] articleList)
        {
            if (articleList?.FirstOrDefault() == null)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                this.Invoke(new PackStoredEventHandler(StorageSystem_PackStored), sender, articleList);
                return;
            }

            foreach (var article in articleList)
            {
                foreach (var pack in article.Packs)
                {
                    WriteInputLog("Pack '{0}' for article '{1}' was stored.", pack.Id, article.Id);
                }
            }

            _stockModel.Update(articleList, false);

            this.LoadStockInfoDataGrids();
        }

        /// <summary>
        /// Event which is raised whenever a pack was dispensed by an output
        /// process that was not initiated by this storage system connection (e.g. at the UI of the storage system).
        /// </summary>
        /// <param name="sender">Object instance which raised the event.</param>
        /// <param name="articles">List of articles with the packs that were dispensed.</param>
        private void StorageSystem_PackDispensed(IStorageSystem sender, IArticle[] articles)
        {
            if ((articles == null) || (articles.Length == 0))
            {
                return;
            }

            if (this.InvokeRequired)
            {
                this.Invoke(new PackDispensedEventHandler(StorageSystem_PackDispensed), sender, articles);
                return;
            }

            foreach (var article in articles)
            {
                foreach (var pack in article.Packs)
                {
                    WriteInputLog("Pack '{0}' for article '{1}' was dispensed.", pack.Id, article.Id);
                }
            }

            _stockModel.Remove(articles);

            this.LoadStockInfoDataGrids();
        }

        /// <summary>
        /// Event which is thrown when packs were dispensed by an output job.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Dispensing event args.</param>
        private void OrderModel_PackDispensed(object sender, EventArgs e)
        {
            _stockModel.Remove(((PackDispensedArgs)e).Packs);

            this.LoadStockInfoDataGrids();
        }

        /// <summary>
        /// Event which is thrown when a box is released.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Box releasing event args.</param>
        private void OrderModel_BoxReleased(object sender, EventArgs e)
        {
            dataGridOrderBoxes.DataSource = null;

            dataGridOrderBoxes.DataSource = ((BoxReleasedArgs)e).Boxes;
        }

        /// <summary>
        /// Writes the specified input log.
        /// </summary>
        /// <param name="format">The format string of the log entry.</param>
        /// <param name="args">The arguments of the log entry.</param>
        private void WriteInputLog(string format, params object[] args)
        {
            if (InvokeRequired)
            {
                Invoke(new WriteLog((f, a) => { WriteInputLog(f, a); }), format, args);
                return;
            }

            string logMessage = string.Format(format, args);
            listInputLog.TopIndex = listInputLog.Items.Add(string.Format("{0:HH:mm:ss,fff}  {1}", DateTime.Now, logMessage));
        }

        /// <summary>
        /// Writes the specified initiated input log.
        /// </summary>
        /// <param name="format">The format string of the log entry.</param>
        /// <param name="args">The arguments of the log entry.</param>
        private void WriteInitiateInputLog(string format, params object[] args)
        {
            if (InvokeRequired)
            {
                Invoke(new WriteLog((f, a) => { WriteInitiateInputLog(f, a); }), format, args);
                return;
            }

            string logMessage = string.Format(format, args);
            listInitInputLog.TopIndex = listInitInputLog.Items.Add(string.Format("{0:HH:mm:ss,fff}  {1}", DateTime.Now, logMessage));
        }

        /// <summary>
        /// processs DigitalShelf ArticleInfoRequested message
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="request">request message</param>
        private void DigitalShelf_ArticleInfoRequested(IDigitalShelf sender, IArticleInfoRequest request)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new ArticleInfoRequestEventHandler(DigitalShelf_ArticleInfoRequested),
                    new object[] { sender, request });
                return;
            }

            listBoxDigitalShelfLog.Items.Add(string.Format("Article information for '{0}' articles has been requested from the digital shelf.", request.Articles.Length));

            foreach (var article in request.Articles)
            {
                article.SetArticleInformation(article.Id, textBoxArticleName.Text, textBoxDosageForm.Text, textBoxPackagingUnit.Text, (uint)numericUpDownMaxSubItemQuantity.Value);

                foreach (var tag in listBoxTag.Items)
                {
                    article.AddTag((string)tag);
                }

                if (request.IncludeCrossSellingArticles)
                {
                    foreach (var crossSellingArticleId in listBoxCrossSellingArticle.Items)
                    {
                        article.AddCrossSellingArticle((string)crossSellingArticleId);
                    }
                }

                if (request.IncludeAlternativeArticles)
                {
                    foreach (var alternativeArticleId in listBoxAlternativeArticle.Items)
                    {
                        article.AddAlternativeArticle((string)alternativeArticleId);
                    }
                }

                if (request.IncludeAlternativePackSizeArticles)
                {
                    foreach (var AlternativePackSizeArticleId in listBoxAlternativePackSizeArticle.Items)
                    {
                        article.AddAlternativePackSizeArticle((string)AlternativePackSizeArticleId);
                    }
                }
            }

            request.Finish();
        }

        /// <summary>
        /// processs DigitalShelf ArticlePriceRequested message
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="request">request message</param>
        private void DigitalShelf_ArticlePriceRequested(IDigitalShelf sender, IArticlePriceRequest request)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new ArticlePriceRequestEventHandler(DigitalShelf_ArticlePriceRequested),
                    new object[] { sender, request });
                return;
            }

            listBoxDigitalShelfLog.Items.Add(string.Format("Price information for '{0}' articles has been requested from the digital shelf.", request.Articles.Length));

            foreach (var article in request.Articles)
            {
                foreach (var priceInformationItem in listBoxPriceInformation.Items)
                {
                    String[] priceInformations = ((string)priceInformationItem).Split(':');

                    PriceCategory priceCategory;
                    Enum.TryParse<PriceCategory>(priceInformations[0].Trim(), out priceCategory);

                    article.AddPriceInformation(priceCategory,
                        decimal.Parse(priceInformations[1].Trim()),
                        uint.Parse(priceInformations[2].Trim()),
                        decimal.Parse(priceInformations[3].Trim()),
                        priceInformations[4].Trim(),
                        decimal.Parse(priceInformations[5].Trim()),
                        priceInformations[6].Trim());
                }
            }

            request.Finish();
        }

        /// <summary>
        /// processs DigitalShelf ArticleSelected message
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="request">request message</param>
        private void DigitalShelf_ArticleSelected(IDigitalShelf sender, IDigitalShelfArticle article)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new ArticleSelectedEventHandler(DigitalShelf_ArticleSelected),
                    new object[] { sender, article });
                return;
            }

            listBoxDigitalShelfLog.Items.Add(string.Format("Article '{0} - {1}' has been selected on the digital shelf screen.", article.Id, article.Name));
        }

        /// <summary>
        /// processs DigitalShelf ShoppingCartRequested message
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="request">request message</param>
        private void DigitalShelf_ShoppingCartRequested(IDigitalShelf sender, IShoppingCartRequest request)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new ShoppingCartRequestEventHandler(DigitalShelf_ShoppingCartRequested),
                    new object[] { sender, request });
                return;
            }

            listBoxDigitalShelfLog.Items.Add(string.Format("Shopping cart has been requested from the digital shelf."));
            var shoppingCart = _digitalShelf.CreateShoppingCart(
                String.IsNullOrEmpty(request.Criteria.ShoppingCartId) ? new Random().Next().ToString() : request.Criteria.ShoppingCartId,
                (ShoppingCartStatus)comboBoxShoppingCartStatus.SelectedItem,
                string.IsNullOrEmpty(textBoxShoppingCartCustomerID.Text) ? request.Criteria.CustomerId : textBoxShoppingCartCustomerID.Text,
                string.IsNullOrEmpty(textBoxShoppingCartSalesPersonId.Text) ? request.Criteria.SalesPersonId : textBoxShoppingCartSalesPersonId.Text,
                string.IsNullOrEmpty(textBoxShoppingCartSalesPointID.Text) ? request.Criteria.SalesPointId : textBoxShoppingCartSalesPointID.Text,
                string.IsNullOrEmpty(textBoxShoppingCartViewPointID.Text) ? request.Criteria.ViewPointId : textBoxShoppingCartViewPointID.Text);

            foreach (ListViewItem item in listViewShoppingCartItemsForRequest.Items)
            {
                String[] ShoppingCartItemInformations = ((string)item.Tag).Split(':');

                shoppingCart.AddItem(ShoppingCartItemInformations[0],
                    uint.Parse(ShoppingCartItemInformations[3]),
                    uint.Parse(ShoppingCartItemInformations[2]),
                    uint.Parse(ShoppingCartItemInformations[4]),
                    ShoppingCartItemInformations[5],
                    ShoppingCartItemInformations[1]);
            }

            if (radioButtonShoppingCartAccept.Checked)
            {
                request.Accept(shoppingCart);
                listBoxDigitalShelfLog.Items.Add(string.Format("Shopping cart (ID:{0}) has been accepted from the digital shelf.", shoppingCart.Id));
            }
            else
            {
                request.Reject();
                listBoxDigitalShelfLog.Items.Add(string.Format("Shopping cart has been rejected from the digital shelf."));
            }

        }

        /// <summary>
        /// Handles the Click event of the buttonAddShoppingCartItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonAddShoppingCartItem_Click(object sender, EventArgs e)
        {
            FAddShoppingCartItem addShoppingCartItem = new FAddShoppingCartItem();
            if (addShoppingCartItem.ShowDialog(this) == DialogResult.OK)
            {
                ListViewItem listNewItem = new ListViewItem(addShoppingCartItem.ArticleId);
                listNewItem.SubItems.Add(addShoppingCartItem.Currency);
                listNewItem.SubItems.Add(addShoppingCartItem.DispensedQuantity.ToString());
                listNewItem.SubItems.Add(addShoppingCartItem.OrderedQuantity.ToString());
                listNewItem.SubItems.Add(addShoppingCartItem.PaidQuantity.ToString());
                listNewItem.SubItems.Add(addShoppingCartItem.Price.ToString());
                listNewItem.Tag = addShoppingCartItem.ArticleId + ":" + addShoppingCartItem.Currency + ":" +
                                  addShoppingCartItem.DispensedQuantity + ":"
                                  + addShoppingCartItem.OrderedQuantity + ":" + addShoppingCartItem.PaidQuantity + ":" +
                                  addShoppingCartItem.Price;
                listViewShoppingCartItemsForRequest.Items.Add(listNewItem);
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonRemovePriceInformation control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonRemoveShoppingCartItem_Click(object sender, EventArgs e)
        {
            ListView.SelectedListViewItemCollection selectedItems = listViewShoppingCartItemsForRequest.SelectedItems;
            for (int i = selectedItems.Count - 1; i >= 0; i--)
            {
                listViewShoppingCartItemsForRequest.Items.Remove(selectedItems[i]);
            }
        }

        /// <summary>
        /// processs DigitalShelf ShoppingCartUpdateRequested message
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="request">request message</param>
        private void DigitalShelf_ShoppingCartUpdateRequested(IDigitalShelf sender, IShoppingCartUpdateRequest request)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new ShoppingCartUpdateRequestEventHandler(DigitalShelf_ShoppingCartUpdateRequested),
                    new object[] { sender, request });
                return;
            }

            listBoxDigitalShelfLog.Items.Add(string.Format("Shopping cart update has been requested from the digital shelf."));

            textBoxShoppingCartInfoID.Text = request.ShoppingCart.Id;
            comboBoxShoppingCartInfoStatus.SelectedItem = request.ShoppingCart.Status;
            textBoxShoppingCartInfoCustomerID.Text = request.ShoppingCart.CustomerId;
            textBoxShoppingCartInfoSalesPersonID.Text = request.ShoppingCart.SalesPersonId;
            textBoxShoppingCartInfoSalesPointID.Text = request.ShoppingCart.SalesPointId;
            textBoxShoppingCartInfoViewPointID.Text = request.ShoppingCart.ViewPointId;

            listViewShoppingCartItemsForUpdate.Items.Clear();
            foreach (IShoppingCartItem item in request.ShoppingCart.ShoppingCartItems)
            {
                ListViewItem listNewItem = new ListViewItem(item.ArticleId);
                listNewItem.SubItems.Add(item.Currency);
                listNewItem.SubItems.Add(item.DispensedQuantity.ToString());
                listNewItem.SubItems.Add(item.OrderedQuantity.ToString());
                listNewItem.SubItems.Add(item.PaidQuantity.ToString());
                listNewItem.SubItems.Add(item.Price.ToString());
                listViewShoppingCartItemsForUpdate.Items.Add(listNewItem);
            }

            _currentShoppingCartUpdateRequest = request;

            if (radioButtonShoppingCartUpdateAutoAccept.Checked)
            {
                buttonShoppingCartUpdateAutoAccept_Click(null, null);
            }
            else if (radioButtonShoppingCartUpdateAutoReject.Checked)
            {
                buttonShoppingCartUpdateAutoReject_Click(null, null);
                listViewShoppingCartItemsForUpdate.Items.RemoveAt(listViewShoppingCartItemsForUpdate.Items.Count - 1);
            }
            buttonShoppingCartUpdateAutoAccept.Enabled = _currentShoppingCartUpdateRequest != null;
            buttonShoppingCartUpdateAutoReject.Enabled = _currentShoppingCartUpdateRequest != null;
        }

        /// <summary>
        /// Handles the Click event of the  buttonShoppingCartUpdateAutoAccept control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonShoppingCartUpdateAutoAccept_Click(object sender, EventArgs e)
        {
            _currentShoppingCartUpdateRequest.Accept(textBoxShoppingCartUpdateHandlingDescription.Text);
            _currentShoppingCartUpdateRequest = null;
            buttonShoppingCartUpdateAutoAccept.Enabled = false;
            buttonShoppingCartUpdateAutoReject.Enabled = false;
        }

        /// <summary>
        /// Handles the Click event of the  buttonShoppingCartUpdateAutoReject control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonShoppingCartUpdateAutoReject_Click(object sender, EventArgs e)
        {
            _currentShoppingCartUpdateRequest.Reject(_currentShoppingCartUpdateRequest.ShoppingCart, textBoxShoppingCartUpdateHandlingDescription.Text);
            _currentShoppingCartUpdateRequest = null;
            buttonShoppingCartUpdateAutoAccept.Enabled = false;
            buttonShoppingCartUpdateAutoReject.Enabled = false;
        }

        /// <summary>
        /// Handles the Click event of the  buttonClearInputLog control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonClearInputLog_Click(object sender, EventArgs e)
        {
            listBoxDigitalShelfLog.Items.Clear();
        }

        /// <summary>
        /// Handles the Click event of the  buttonDigitalShelfConnect control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonDigitalShelfConnect_Click(object sender, EventArgs e)
        {
            buttonDigitalShelfDisconnect_Click(sender, e);
            buttonDigitalShelfConnect.Enabled = false;

            _digitalShelf = new RowaDigitalShelf();
            _digitalShelf.ArticleInfoRequested += DigitalShelf_ArticleInfoRequested;
            _digitalShelf.ArticlePriceRequested += DigitalShelf_ArticlePriceRequested;
            _digitalShelf.ArticleSelected += DigitalShelf_ArticleSelected;
            _digitalShelf.ShoppingCartRequested += DigitalShelf_ShoppingCartRequested;
            _digitalShelf.ShoppingCartUpdateRequested += DigitalShelf_ShoppingCartUpdateRequested;

            var connectParams = new string[] { textBoxDigitalShelfAddress.Text, numericUpDownDigitalShelfPort.Value.ToString() };
            backgroundWorkerConnectDigitalShelf.RunWorkerAsync(connectParams);
        }

        /// <summary>
        /// Handles the Click event of the  buttonDigitalShelfDisconnect control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonDigitalShelfDisconnect_Click(object sender, EventArgs e)
        {
            buttonDigitalShelfDisconnect.Enabled = false;
            _digitalShelf.Disconnect();
            _digitalShelf.ArticleInfoRequested -= DigitalShelf_ArticleInfoRequested;
            _digitalShelf.ArticlePriceRequested -= DigitalShelf_ArticlePriceRequested;
            _digitalShelf.ArticleSelected -= DigitalShelf_ArticleSelected;
            _digitalShelf.ShoppingCartRequested -= DigitalShelf_ShoppingCartRequested;
            _digitalShelf.ShoppingCartUpdateRequested -= DigitalShelf_ShoppingCartUpdateRequested;

            _digitalShelf.Dispose();
            buttonDigitalShelfConnect.Enabled = true;
            btnSendRawXmlDigitalShelf.Enabled = false;
        }

        /// <summary>
        /// Handles the DoWork event of the backgroundWorkerConnectDigitalShelf control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        private void backgroundWorkerConnectDigitalShelf_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                var connectParams = (string[])e.Argument;
                _digitalShelf.Connect(connectParams[0], ushort.Parse(connectParams[1]));
                e.Result = true;
            }
            catch (Exception)
            {
                e.Result = false;
            }
        }

        /// <summary>
        /// Handles the RunWorkerCompleted event of the backgroundWorkerConnectDigitalShelf control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        private void backgroundWorkerConnectDigitalShelf_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            if ((e.Result == null) || ((bool)e.Result == false))
            {
                MessageBox.Show(string.Format("Connecting to digital shelf '{0}' failed.", textBoxDigitalShelfAddress.Text),
                                "IT System Simulator");

                buttonDigitalShelfConnect.Enabled = true;
            }
            else
            {
                // store as last known working
                using (var key = Registry.CurrentUser.CreateSubKey("Software\\CareFusion\\ITSystemSimulator"))
                {
                    key.SetValue("DigitalShelfAddress", textBoxDigitalShelfAddress.Text);
                    key.SetValue("DigitalShelfPort", (int)numericUpDownDigitalShelfPort.Value, RegistryValueKind.DWord);
                }
                buttonDigitalShelfDisconnect.Enabled = true;
                btnSendRawXmlDigitalShelf.Enabled = true;
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonAddTag control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonAddTag_Click(object sender, EventArgs e)
        {
            FArticleId newArticleID = new FArticleId();
            if (newArticleID.ShowDialog(this) == DialogResult.OK)
            {
                listBoxTag.Items.Add(newArticleID.ArticleId);
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonRemoveTag control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonRemoveTag_Click(object sender, EventArgs e)
        {
            ListBox.SelectedObjectCollection selectedItems = new ListBox.SelectedObjectCollection(listBoxTag);
            selectedItems = listBoxTag.SelectedItems;
            for (int i = selectedItems.Count - 1; i >= 0; i--)
            {
                listBoxTag.Items.Remove(selectedItems[i]);
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonAddCrossSellingArticle control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonAddCrossSellingArticle_Click(object sender, EventArgs e)
        {
            FArticleId newArticleID = new FArticleId();
            if (newArticleID.ShowDialog(this) == DialogResult.OK)
            {
                listBoxCrossSellingArticle.Items.Add(newArticleID.ArticleId);
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonRemoveCrossSellingArticle control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonRemoveCrossSellingArticle_Click(object sender, EventArgs e)
        {
            ListBox.SelectedObjectCollection selectedItems = new ListBox.SelectedObjectCollection(listBoxCrossSellingArticle);
            selectedItems = listBoxCrossSellingArticle.SelectedItems;
            for (int i = selectedItems.Count - 1; i >= 0; i--)
            {
                listBoxCrossSellingArticle.Items.Remove(selectedItems[i]);
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonAddAlternativeArticle control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonAddAlternativeArticle_Click(object sender, EventArgs e)
        {
            FArticleId newArticleID = new FArticleId();
            if (newArticleID.ShowDialog(this) == DialogResult.OK)
            {
                listBoxAlternativeArticle.Items.Add(newArticleID.ArticleId);
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonRemoveAlternativeArticle control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonRemoveAlternativeArticle_Click(object sender, EventArgs e)
        {
            ListBox.SelectedObjectCollection selectedItems = new ListBox.SelectedObjectCollection(listBoxAlternativeArticle);
            selectedItems = listBoxAlternativeArticle.SelectedItems;
            for (int i = selectedItems.Count - 1; i >= 0; i--)
            {
                listBoxAlternativeArticle.Items.Remove(selectedItems[i]);
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonAddAlternativePackSizeArticle control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonAddAlternativePackSizeArticle_Click(object sender, EventArgs e)
        {
            FArticleId newArticleID = new FArticleId();
            if (newArticleID.ShowDialog(this) == DialogResult.OK)
            {
                listBoxAlternativePackSizeArticle.Items.Add(newArticleID.ArticleId);
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonRemoveAlternativePackSizeArticle control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonRemoveAlternativePackSizeArticle_Click(object sender, EventArgs e)
        {
            ListBox.SelectedObjectCollection selectedItems = new ListBox.SelectedObjectCollection(listBoxAlternativePackSizeArticle);
            selectedItems = listBoxAlternativePackSizeArticle.SelectedItems;
            for (int i = selectedItems.Count - 1; i >= 0; i--)
            {
                listBoxAlternativePackSizeArticle.Items.Remove(selectedItems[i]);
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonAddPriceInformation control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonAddPriceInformation_Click(object sender, EventArgs e)
        {
            FAddPriceInformation addPriceInformation = new FAddPriceInformation();
            if (addPriceInformation.ShowDialog(this) == DialogResult.OK)
            {
                listBoxPriceInformation.Items.Add(
                    string.Format("{0} : {1} : {2} : {3} : {4} : {5} : {6}",
                        addPriceInformation.PriceCategory.ToString(),
                        addPriceInformation.Price,
                        addPriceInformation.Quantity,
                        addPriceInformation.BasePrice,
                        addPriceInformation.BasePriceUnit,
                        addPriceInformation.VAT,
                        addPriceInformation.Description));

            }
        }

        /// <summary>
        /// Handles the Click event of the buttonRemovePriceInformation control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonRemovePriceInformation_Click(object sender, EventArgs e)
        {
            ListBox.SelectedObjectCollection selectedItems = new ListBox.SelectedObjectCollection(listBoxPriceInformation);
            selectedItems = listBoxPriceInformation.SelectedItems;
            for (int i = selectedItems.Count - 1; i >= 0; i--)
            {
                listBoxPriceInformation.Items.Remove(selectedItems[i]);
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonClearDigitalShelfLog control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void buttonClearDigitalShelfLog_Click(object sender, EventArgs e)
        {
            listInputLog.Items.Clear();
        }

        /// <summary>
        /// Handles the Click event of the checkBoxTagAutoGenerate control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void checkBoxTagAutoGenerate_Click(object sender, EventArgs e)
        {
            listBoxTag.Enabled = !checkBoxTagAutoGenerate.Checked;
            buttonAddTag.Enabled = !checkBoxTagAutoGenerate.Checked;
            buttonRemoveTag.Enabled = !checkBoxTagAutoGenerate.Checked;

            listBoxTag.Items.Clear();

            if (checkBoxTagAutoGenerate.Checked)
            {
                listBoxTag.Items.Add("123");
                listBoxTag.Items.Add("124");
                listBoxTag.Items.Add("125");
            }
        }

        /// <summary>
        /// Handles the Click event of the checkBoxCrossSellingArticleAutoGenerate control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void checkBoxCrossSellingArticleAutoGenerate_Click(object sender, EventArgs e)
        {
            listBoxCrossSellingArticle.Enabled = !checkBoxCrossSellingArticleAutoGenerate.Checked;
            buttonAddCrossSellingArticle.Enabled = !checkBoxCrossSellingArticleAutoGenerate.Checked;
            buttonRemoveCrossSellingArticle.Enabled = !checkBoxCrossSellingArticleAutoGenerate.Checked;

            listBoxCrossSellingArticle.Items.Clear();

            if (checkBoxCrossSellingArticleAutoGenerate.Checked)
            {
                listBoxCrossSellingArticle.Items.Add("223");
                listBoxCrossSellingArticle.Items.Add("224");
                listBoxCrossSellingArticle.Items.Add("225");
            }
        }

        /// <summary>
        /// Handles the Click event of the checkBoxAlternativeArticlesAutoGenerate control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void checkBoxAlternativeArticlesAutoGenerate_Click(object sender, EventArgs e)
        {
            listBoxAlternativeArticle.Enabled = !checkBoxAlternativeArticlesAutoGenerate.Checked;
            buttonAddAlternativeArticle.Enabled = !checkBoxAlternativeArticlesAutoGenerate.Checked;
            buttonRemoveAlternativeArticle.Enabled = !checkBoxAlternativeArticlesAutoGenerate.Checked;

            listBoxAlternativeArticle.Items.Clear();

            if (checkBoxAlternativeArticlesAutoGenerate.Checked)
            {
                listBoxAlternativeArticle.Items.Add("323");
                listBoxAlternativeArticle.Items.Add("324");
                listBoxAlternativeArticle.Items.Add("325");
            }
        }

        /// <summary>
        /// Handles the Click event of the checkBoxAlternativePackSizeAutoGenerated control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void checkBoxAlternativePackSizeAutoGenerated_Click(object sender, EventArgs e)
        {
            listBoxAlternativePackSizeArticle.Enabled = !checkBoxAlternativeArticlesAutoGenerate.Checked;
            buttonAddAlternativePackSizeArticle.Enabled = !checkBoxAlternativeArticlesAutoGenerate.Checked;
            buttonRemoveAlternativePackSizeArticle.Enabled = !checkBoxAlternativeArticlesAutoGenerate.Checked;

            listBoxAlternativePackSizeArticle.Items.Clear();

            if (checkBoxAlternativeArticlesAutoGenerate.Checked)
            {
                listBoxAlternativePackSizeArticle.Items.Add("423");
                listBoxAlternativePackSizeArticle.Items.Add("424");
                listBoxAlternativePackSizeArticle.Items.Add("425");
            }
        }

        /// <summary>
        /// Handles the Click event of the checkBoxPriceInformationAutoGenerate control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void checkBoxPriceInformationAutoGenerate_Click(object sender, EventArgs e)
        {
            listBoxPriceInformation.Enabled = !checkBoxPriceInformationAutoGenerate.Checked;
            buttonAddPriceInformation.Enabled = !checkBoxPriceInformationAutoGenerate.Checked;
            buttonRemovePriceInformation.Enabled = !checkBoxPriceInformationAutoGenerate.Checked;

            listBoxPriceInformation.Items.Clear();

            if (checkBoxPriceInformationAutoGenerate.Checked)
            {
                listBoxPriceInformation.Items.Add(string.Format("{0} : {1} : {2} : {3} : {4} : {5} : {6}", PriceCategory.RRP.ToString(), 100, 1, 100, "EUR", 5, "Base price"));
                listBoxPriceInformation.Items.Add(string.Format("{0} : {1} : {2} : {3} : {4} : {5} : {6}", PriceCategory.Offer.ToString(), 80, 2, 100, "EUR", 5, "two for 80%"));
                listBoxPriceInformation.Items.Add(string.Format("{0} : {1} : {2} : {3} : {4} : {5} : {6}", PriceCategory.Other.ToString(), 50, 100, 100, "EUR", 5, string.Empty));
            }
        }

        /// <summary>
        /// Handles the Click event of the checkBoxArticleInformationAutoGenerate control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void checkBoxArticleInformationAutoGenerate_Click(object sender, EventArgs e)
        {
            textBoxArticleName.Enabled = !checkBoxArticleInformationAutoGenerate.Checked;
            textBoxDosageForm.Enabled = !checkBoxArticleInformationAutoGenerate.Checked;
            textBoxPackagingUnit.Enabled = !checkBoxArticleInformationAutoGenerate.Checked;
            numericUpDownMaxSubItemQuantity.Enabled = !checkBoxArticleInformationAutoGenerate.Checked;

            if (checkBoxAlternativeArticlesAutoGenerate.Checked)
            {
                textBoxArticleName.Text = "BD | Digital Shelf Article";
                textBoxDosageForm.Text = string.Empty;
                textBoxPackagingUnit.Text = string.Empty;
                numericUpDownMaxSubItemQuantity.Value = 10;
            }
        }

        private void dataGridOrderBoxes_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.Cancel = true;
        }

        private void btnClearRawXml_Click(object sender, EventArgs e)
        {
            txtRawXml.Clear();
        }

        private void btnClearRawXmlDigitalShelf_Click(object sender, EventArgs e)
        {
            txtRawXmlDigitalShelf.Clear();
        }

        private void txtRawXml_TextChanged(object sender, EventArgs e)
        {
            btnSendRawXml.Enabled = (String.IsNullOrWhiteSpace(txtRawXml.Text) ? false : true)
                                    && (_storageSystem != null && _storageSystem.Connected);
        }

        private void btnSendRawXml_Click(object sender, EventArgs e)
        {
            if (_storageSystem != null && _storageSystem.Connected)
            {
                _storageSystem.SendRawXml(System.Text.Encoding.UTF8.GetBytes(txtRawXml.Text));
            }
        }

        private void btnSendRawXmlDigitalShelf_Click(object sender, EventArgs e)
        {
            if (_digitalShelf != null && _digitalShelf.Connected)
            {
                _digitalShelf.SendRawXml(System.Text.Encoding.UTF8.GetBytes(txtRawXmlDigitalShelf.Text));
            }
        }

        /// <summary>
        /// Handles the Click event of the checkAutoConnect control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void checkAutoConnect_CheckedChanged(object sender, EventArgs e)
        {
            if (checkAutoConnect.Checked)
            {
                try
                {
                    btnConnect.Enabled = false;
                    ConnectStorageSystem();
                }
                catch (Exception ex)
                {
                    ShowErrorMessage("Storage connection failed", ex);
                }
            }
            else
            {
                btnConnect.Enabled = !_storageSystem?.Connected ?? false;
            }
        }

        private void ParseDatamatrixCode(IInputPack pack, InputArticle article)
        {
            foreach (IScancodeParser codeParser in _codeParserList)
            {
                try
                {
                    ScancodeParseResult parseResult = codeParser.Parse(pack.ScanCode);

                    if (string.IsNullOrEmpty(parseResult.ItemCode))
                        continue;
                    if (Scancode.TryGetPzn(parseResult.ItemCode, out var pzn))
                    {
                        article.Id = pzn;
                    }
                    else
                    {
                        article.Id = parseResult.ItemCode;
                    }

                    pack.SetPackInformation(!string.IsNullOrEmpty(parseResult.BatchNumber) ? parseResult.BatchNumber : pack.BatchNumber,
                                                parseResult.ExternalID,
                                                parseResult.ExpiryDate.HasValue ? parseResult.ExpiryDate.Value.DateTime : pack.ExpiryDate,
                                                parseResult.SubItemQuantity > default(int) ? (uint)parseResult.SubItemQuantity : pack.SubItemQuantity,
                                                pack.StockLocationId,
                                                !string.IsNullOrEmpty(parseResult.SerialNumber) ? parseResult.SerialNumber : pack.SerialNumber);

                    break;
                }
                catch (ScancodeParseException exception)
                {
                    this.Error(exception.Message);
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the btnCloneOrder control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void BtnCloneOrder_Click(object sender, EventArgs e)
        {
            if (dataGridOrders.CurrentRow != null)
            {
                try
                {
                    var orderToClone = _orderModel._orderList[dataGridOrders.CurrentRow.Index];
                    var newClonedOrder = CloneExistingOrder(orderToClone);
                    _orderModel.AddOrder(newClonedOrder);
                }
                catch (Exception exception)
                {
                    this.Error(exception.Message);
                }
            }
        }

        /// <summary>
        /// Clone an existing output order.
        /// </summary>
        /// <returns>Newly created order or null.</returns>
        private IOutputProcess CloneExistingOrder(IOutputProcess orderToClone)
        {
            const int maxValue = 10000;
            string boxNumber = string.Empty;
            var orderNumber = orderToClone.OrderNumber + "_c" + new Random().Next(maxValue);

            if (!string.IsNullOrEmpty(orderToClone.BoxNumber))
            {
                var newBoxNumberForm = new FNewBoxNumber();
                newBoxNumberForm.ShowDialog(this);
                boxNumber = newBoxNumberForm.NewBoxNumberValue;
            }

            var result = _storageSystem.CreateOutputProcess(orderNumber,
                                                            orderToClone.OutputDestination,
                                                            orderToClone.OutputPoint,
                                                            orderToClone.Priority,
                                                            boxNumber);

            foreach (var criteria in orderToClone.Criteria)
            {
                result.AddCriteria(criteria.ArticleId,
                                   criteria.Quantity,
                                   criteria.BatchNumber,
                                   criteria.ExternalId,
                                   criteria.MinimumExpiryDate,
                                   criteria.PackId,
                                   criteria.SubItemQuantity);
            }

            for (int i = 0; i < orderToClone.Criteria.Length; ++i)
            {
                foreach (var label in orderToClone.Criteria[i].Labels)
                {
                    result.Criteria[i].AddLabel(label.TemplateId, label.Content);
                }
            }

            return result;
        }

        /// <summary>
        /// Handles the Click event of the btnSendInfeedInputRequest control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnSendInfeedInputRequest_Click(object sender, EventArgs e)
        {
            btnSendInfeedInputRequest.Enabled = false;
            btnInfeedPackPlaced.Enabled = false;
            btnInfeedInputAbort.Enabled = false;

            var request = _storageSystem.CreateInfeedInputRequest(numInfeedInputID.Value.ToString(),
                                                                  (int)numInfeedNumber.Value,
                                                                  (int)numInfeedDestination.Value,
                                                                  string.IsNullOrEmpty(txtInfeedDeliveryNumber.Text) ? null : txtInfeedDeliveryNumber.Text,
                                                                  chkInfeedSetPickingIndicator.Checked);

            if (request == null)
            {
                MessageBox.Show("Infeed input is not supported by the storage system.");
                return;
            }

            request.PlacePacks += OnInfeedInput_PlacePacks;

            Interlocked.Exchange(ref _activeInfeedInput, request);

            DateTime? expiryDate = null;
            DateTime expiryDateCheck;

            if (DateTime.TryParse(txtInfeedPackExpiryDate.Text, out expiryDateCheck))
                expiryDate = expiryDateCheck;

            var expiryDateSourceSelectedValue = !(this.cmbInfeedPackExpiryDateSource.SelectedValue is ComboboxItem comboboxItem)
                ? (string)this.cmbInfeedPackExpiryDateSource.SelectedValue
                : comboboxItem.Value;

            request.AddInputPack(txtInfeedPackScancode.Text,
                txtInfeedPackBatchNumber.Text,
                null,
                expiryDate,
                (ExpiryDateSource)Enum.Parse(typeof(ExpiryDateSource), expiryDateSourceSelectedValue),
                (int)numInfeedPackSubItemQuantity.Value,
                (int)numInfeedPackDepth.Value,
                (int)numInfeedPackWidth.Value,
                (int)numInfeedPackHeight.Value,
                (PackShape)Enum.Parse(typeof(PackShape), cmbInfeedPackShape.Text),
                string.IsNullOrEmpty(txtInfeedPackStockLocation.Text) ? null : txtInfeedPackStockLocation.Text,
                null,
                txtInfeedPackSerialNumber.Text);

            bgInfeedInput.RunWorkerAsync(request);
        }

        /// <summary>
        /// Handles the Click event of the btnInfeedPackPlaced control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnInfeedPackPlaced_Click(object sender, EventArgs e)
        {
            btnInfeedPackPlaced.Enabled = false;
            btnInfeedInputAbort.Enabled = false;

            _activeInfeedInput?.PacksPlaced();
        }

        /// <summary>
        /// Handles the Click event of the btnInfeedInputAbort control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnInfeedInputAbort_Click(object sender, EventArgs e)
        {
            btnInfeedPackPlaced.Enabled = false;
            btnInfeedInputAbort.Enabled = false;

            _activeInfeedInput?.Abort();
        }

        /// <summary>
        /// Handles the Click event of the btnClearInfeedInputLog control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnClearInfeedInputLog_Click(object sender, EventArgs e)
        {
            listInfeedInputLog.Items.Clear();
        }

        /// <summary>
        /// Handles the DoWork event of the bgInfeedInput control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        private void bgInfeedInput_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            var request = (IInfeedInputRequest)e.Argument;

            bgInfeedInput.ReportProgress(1, $"Sending InfeedInputRequest({request.Id}) ...");

            try
            {
                using (var finishedEvent = new ManualResetEvent(false))
                {
                    request.Finished += (s, o) => finishedEvent.Set();
                    request.Start();

                    bgInfeedInput.ReportProgress(1, $"InfeedInputRequest({request.Id}) successfully sent.");
                    bgInfeedInput.ReportProgress(1, $"  -> Selected InfeedNumber = {request.InfeedNumber}");

                    finishedEvent.WaitOne();
                    e.Result = true;
                }
            }
            catch (Exception ex)
            {
                bgInfeedInput.ReportProgress(1, $"Processing InfeedInputRequest({request.Id}) failed with error '{ex.Message}'.");
                e.Result = ex;
            }
        }

        /// <summary>
        /// Handles the ProgressChanged event of the bgInfeedInput control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        private void bgInfeedInput_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            if (e.UserState != null)
                WriteInfeedInputLog((string)e.UserState);
        }

        /// <summary>
        /// Handles the RunWorkerCompleted event of the bgInfeedInput control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        private void bgInfeedInput_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            btnInfeedPackPlaced.Enabled = false;
            btnInfeedInputAbort.Enabled = false;

            if ((e.Result != null) && (e.Result is Exception))
            {
                MessageBox.Show($"Processing InfeedInputRequest({_activeInfeedInput.Id}) failed.", "IT System Simulator");
            }

            WriteInfeedInputLog($"InfeedInputRequest({_activeInfeedInput.Id}) finished with result '{_activeInfeedInput.State}'.");

            if (_activeInfeedInput.State == InfeedInputRequestState.Completed)
            {
                foreach (var article in _activeInfeedInput.InputArticles)
                {
                    WriteInfeedInputLog("  -> Processed Article:\n\tID: '{0}'\n\tName: '{1}'\n\tDosageForm: '{2}'\n\tPackagingUnit: '{3}'.",
                                        article.Id, article.Name, article.DosageForm, article.PackagingUnit);

                    foreach (var pack in article.Packs)
                        WriteInfeedInputLog("  -> Stored Pack with ID '{0}'.", pack.Id);
                }

                if (chbInfeedInputIDAutoInc.Checked && numInfeedInputID.Value < numInfeedInputID.Maximum)
                {
                    numInfeedInputID.Value += 1;
                }
                if (chbInfeedPackSerialNumberAutoInc.Checked)
                {
                    if (!string.IsNullOrEmpty(txtInfeedPackSerialNumber.Text))
                    {
                        int.TryParse(txtInfeedPackSerialNumber.Text, out var valueResult);
                        if (valueResult > 0)
                        {
                            valueResult++;
                            var leadingZerosCount = 0;
                            for (var i = 0;
                                i < txtInfeedPackSerialNumber.Text.Length && txtInfeedPackSerialNumber.Text[i] == '0';
                                i++)
                            {
                                leadingZerosCount++;
                            }

                            leadingZerosCount += valueResult.ToString().Length;
                            txtInfeedPackSerialNumber.Text = valueResult.ToString($"D{leadingZerosCount}");
                        }
                    }
                }
            }
            else if (_activeInfeedInput.State == InfeedInputRequestState.Rejected)
            {
                WriteInfeedInputLog($"  -> Reject Reason '{_activeInfeedInput.RejectionReason}'.");
                WriteInfeedInputLog($"  -> Reject Reason Description '{_activeInfeedInput.RejectionDescription}'.");
            }

            Interlocked.Exchange(ref _activeInfeedInput, null);
            btnSendInfeedInputRequest.Enabled = btnNewOrder.Enabled;
        }

        /// <summary>
        /// Handles the PlacePacks event of an active infeed request.
        /// </summary>
        /// <param name="sender">Infeed request which triggered the event.</param>
        /// <param name="e">Event parameter which are not used here.</param>
        private void OnInfeedInput_PlacePacks(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new EventHandler((f, a) => { OnInfeedInput_PlacePacks(f, a); }), sender, e);
                return;
            }

            if (_activeInfeedInput == null)
                return;

            btnInfeedPackPlaced.Enabled = true;
            btnInfeedInputAbort.Enabled = true;

            WriteInfeedInputLog($"Received InfeedInputPackPlaceRequest for infeed input request '{_activeInfeedInput.Id}'.");
        }

        /// <summary>
        /// Writes the specified initiated input log.
        /// </summary>
        /// <param name="format">The format string of the log entry.</param>
        /// <param name="args">The arguments of the log entry.</param>
        private void WriteInfeedInputLog(string format, params object[] args)
        {
            if (InvokeRequired)
            {
                Invoke(new WriteLog((f, a) => { WriteInfeedInputLog(f, a); }), format, args);
                return;
            }

            string logMessage = string.Format(format, args);
            listInfeedInputLog.TopIndex = listInfeedInputLog.Items.Add(string.Format("{0:HH:mm:ss,fff}  {1}", DateTime.Now, logMessage));
        }

        private void checkEnDisableInputRespons_CheckedChanged(object sender, EventArgs e)
        {
            if (checkEnDisableInputRespons.Checked)
            {
                if (_storageSystem != null)
                    _storageSystem.PackInputRequested += StorageSystem_PackInputRequested;
            }
            else
            {
                if (_storageSystem != null)
                    _storageSystem.PackInputRequested -= StorageSystem_PackInputRequested;
            }

            EnableDisableInputCheckBox(checkEnDisableInputRespons.Checked);
        }

        private void EnableDisableInputCheckBox(bool enable)
        {
            var unAffectedControls = new List<string>
            {
                checkEnDisableInputRespons.Name,
                lblDefExpiryDate.Name,
                numExpiryDateMonth.Name
            };

            foreach (Control control in pnlGeneralInputOptions.Controls)
            {
                var isunAffectedControl = unAffectedControls
                    .Any(i => i.Equals(control.Name, StringComparison.InvariantCultureIgnoreCase));

                if (!isunAffectedControl)
                {
                    control.Enabled = enable;
                }
            }
        }

        /// <summary>
        /// Handles the KeyUp event on the numboxSpecificMaxSubItemQuantity control.
        /// It keeps the recent valid value for the control in order to be restored iff a user leaves it empty on leave event
        /// </summary>
        /// <param name="sender">Infeed request which triggered the event.</param>
        /// <param name="e">Event parameter which are not used here.</param>
        private void numboxSpecificMaxSubItemQuantity_KeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(numboxSpecificMaxSubItemQuantity.Text))
                {
                    Convert.ToInt16(numboxSpecificMaxSubItemQuantity.Text);

                    recentSpecificMaxSubItemQuantity = numboxSpecificMaxSubItemQuantity.Text;
                }
            }
            catch
            {
                numboxSpecificMaxSubItemQuantity.Text = recentSpecificMaxSubItemQuantity;

                numboxSpecificMaxSubItemQuantity.Select(numboxSpecificMaxSubItemQuantity.Text.Length, 0);
            }
        }

        /// <summary>
        /// Handles the Leave event on the numboxSpecificMaxSubItemQuantity control.
        /// Iff text value remains empty, it is set to the recent one, that is kept by Keyup event
        /// </summary>
        /// <param name="sender">Infeed request which triggered the event.</param>
        /// <param name="e">Event parameter which are not used here.</param>
        private void numboxSpecificMaxSubItemQuantity_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(numboxSpecificMaxSubItemQuantity.Text))
            {
                numboxSpecificMaxSubItemQuantity.Text = recentSpecificMaxSubItemQuantity.ToString();
            }
        }

        /// <summary>
        /// Gets the user's following options that should be overriden during an input request:
        /// a. The article's name and
        /// b. The article's subItemMaxQuantity (Random value between 1 and 999, or specific user's value)
        /// </summary>
        /// <returns>An <see cref="IUserInputOptions"/></returns>
        private IUserInputOptions GetUserInputOptions()
        {
            var response = new UserInputOptions
            {
                MaxSubItemQuantity = rbtnRandomMaxSubItemQuantity.Checked
                    ? (uint)new Random().Next(1, 999)
                    : (uint)numboxSpecificMaxSubItemQuantity.Value
            };

            if (checkOverwriteArticleName.Checked)
            {
                response.ArticleName = txtOverwriteArticleName.Text;
            }

            return response;
        }

        /// <summary>
        /// Handles the KeyUp event on the numboxSubscriberId control.
        /// It keeps the recent valid value for the control in order to be restored iff a user leaves it empty on leave event
        /// </summary>
        /// <param name="sender">Infeed request which triggered the event.</param>
        /// <param name="e">Event parameter which are not used here.</param>
        private void numboxSubscriberId_KeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(numboxSubscriberId.Text))
                {
                    Convert.ToInt32(numboxSubscriberId.Text);

                    recentSubscriberId = numboxSubscriberId.Text;
                }
            }
            catch
            {
                numboxSubscriberId.Text = recentSubscriberId;

                numboxSubscriberId.Select(numboxSubscriberId.Text.Length, 0);
            }
        }

        /// <summary>
        /// Handles the Leave event on the numboxSubscriberId control.
        /// Iff text value remains empty, it is set to the recent one, that is kept by Keyup event
        /// </summary>
        /// <param name="sender">Infeed request which triggered the event.</param>
        /// <param name="e">Event parameter which are not used here.</param>
        private void numboxSubscriberId_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(numboxSubscriberId.Text))
            {
                numboxSubscriberId.Text = recentSubscriberId.ToString();
            }
        }

        /// <summary>
        /// Handles the Click event of the btnSendOutputDestinationRequest control.
        /// </summary>
        /// <param name="sender">Infeed request which triggered the event.</param>
        /// <param name="e">Event parameter which are not used here.</param>
        private void btnSendOutputDestinationRequest_Click(object sender, EventArgs e)
        {
            System.Threading.Tasks.Task.Run(() =>
                 Invoke(new Action(() =>
                 {
                     _outputDestinationModel.UpdateStateIndicationResponse(_storageSystem.SetOutputDestinationStateIndication(
                       outputDestination: (int)numericUpOutputDestination.Value,
                       newState: (OutputDestinationState)Enum.Parse(typeof(OutputDestinationState), comboBoxStateIndication.Text)));
                 }))
            );
        }

        /// <summary>
        /// Event which is raised whenever a button is pressed.
        /// </summary>
        /// <param name="sender">Object instance which raised the event.</param>
        /// <param name="outputDestination">The number of the output destination.</param>
        private void StorageSystem_OutputDestinationButtonPressed(IStorageSystem sender, int outputDestination)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new OutputDestinationButtonPressedEventHandler(StorageSystem_OutputDestinationButtonPressed), sender, outputDestination);
                return;
            }

            _outputDestinationModel.UpdateButtonsPressed(outputDestination);
        }

        private void CapabilitiesAvalibleChange(bool enable)
        {
            foreach (var item in allCapabilitiesPanel.Controls)
            {
                if (item is CheckBox)
                {
                    ((CheckBox)item).Enabled = enable;
                }
            }
                    
        }

        #region General methods

        /// <summary>
        /// Clones the specified target object. The new object is totally dettached from its parent, so any changes that might take place, are not passed to its clone child.
        /// </summary>
        /// <typeparam name="T">The type of object for which to prepare its clone</typeparam>
        /// <param name="targetObject">The target object to be cloned.</param>
        /// <returns>A clone object of type T</returns>
        private T Clone<T>(T targetObject)
        {
            var serializedObject = this.SerializeObject(targetObject);

            return this.DeserializeXml<T>(serializedObject);
        }

        /// <summary>
        /// Serializes an object.
        /// </summary>
        /// <typeparam name="T">The type of object to serialize</typeparam>
        /// <param name="targetObject">The target object.</param>
        /// <returns>A string value of the xml serialized object</returns>
        private string SerializeObject<T>(T targetObject)
        {
            if (targetObject == null)
            {
                return default;
            }

            var xmlSerializer = new XmlSerializer(targetObject.GetType());

            using (var stringWriter = new StringWriter())
            {
                xmlSerializer.Serialize(stringWriter, targetObject);

                return stringWriter.ToString();
            }
        }

        /// <summary>
        /// Deserializes an XML to its object prototype.
        /// </summary>
        /// <typeparam name="T">The type of object, being serialized</typeparam>
        /// <param name="target">The target xml string value of the about to serialize object.</param>
        /// <returns>An object of type T</returns>
        private T DeserializeXml<T>(string target)
        {
            if (string.IsNullOrEmpty(target))
            {
                return default;
            }

            var xmlSerializer = new XmlSerializer(typeof(T));

            using (var stringReader = new StringReader(target))
            {
                return (T)xmlSerializer.Deserialize(stringReader);
            }
        }

        #endregion
    }
}