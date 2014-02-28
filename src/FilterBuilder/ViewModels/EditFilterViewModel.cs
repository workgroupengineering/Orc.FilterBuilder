﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EditFilterViewModel.cs" company="Orcomp development team">
//   Copyright (c) 2008 - 2014 Orcomp development team. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------


namespace Orc.FilterBuilder.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Catel;
    using Catel.Data;
    using Catel.MVVM;
    using Orc.FilterBuilder.Models;
    using Orc.FilterBuilder.Services;

    public class EditFilterViewModel : ViewModelBase
    {
        #region Fields
        private readonly FilterScheme _originalFilterScheme;
        private readonly IReflectionService _reflectionService;
        #endregion

        #region Constructors
        public EditFilterViewModel(FilterScheme filterScheme, IReflectionService reflectionService)
        {
            Argument.IsNotNull(() => filterScheme);
            Argument.IsNotNull(() => reflectionService);

            _originalFilterScheme = filterScheme;
            _reflectionService = reflectionService;

            //SuspendValidation = true;
            DeferValidationUntilFirstSaveCall = true;

            InstanceProperties = _reflectionService.GetInstanceProperties(filterScheme.TargetType).Properties;

            FilterScheme = _originalFilterScheme.Copy();
            FilterSchemeTitle = FilterScheme.Title;

            AddGroupCommand = new Command<ConditionGroup>(OnAddGroup);
            AddExpressionCommand = new Command<ConditionGroup>(OnAddExpression);
            DeleteConditionItem = new Command<ConditionTreeItem>(OnDeleteCondition);
        }
        #endregion

        #region Properties
        public override string Title { get { return "Filter scheme"; } }

        public string FilterSchemeTitle { get; set; }
        public FilterScheme FilterScheme { get; private set; }

        public List<IPropertyMetadata> InstanceProperties { get; private set; }

        public Command<ConditionGroup> AddGroupCommand { get; private set; }
        public Command<ConditionGroup> AddExpressionCommand { get; private set; }
        public Command<ConditionTreeItem> DeleteConditionItem { get; private set; }
        #endregion
        
        #region Methods
        protected override void ValidateFields(List<IFieldValidationResult> validationResults)
        {
            if (string.IsNullOrEmpty(FilterSchemeTitle))
            {
                validationResults.Add(FieldValidationResult.CreateError("FilterSchemeTitle", "Field is required"));
            }

            base.ValidateFields(validationResults);
        }

        protected override bool Save()
        {
            FilterScheme.Title = FilterSchemeTitle;
            _originalFilterScheme.Update(FilterScheme);
            
            return true;
        }

        private void OnDeleteCondition(ConditionTreeItem item)
        {
            if (item.Parent == null)
            {
                item.Items.Clear();
            }
            else
            {
                item.Parent.Items.Remove(item);
            }
        }

        private void OnAddExpression(ConditionGroup group)
        {
            var propertyExpression = new PropertyExpression();
            propertyExpression.Property = InstanceProperties.FirstOrDefault();
            group.Items.Add(propertyExpression);
            propertyExpression.Parent = group;
        }

        private void OnAddGroup(ConditionGroup group)
        {
            var newGroup = new ConditionGroup();
            group.Items.Add(newGroup);
            newGroup.Parent = group;
        }
        #endregion
    }
}