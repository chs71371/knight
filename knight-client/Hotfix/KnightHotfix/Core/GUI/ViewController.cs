﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Knight.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Knight.Hotfix.Core
{
    public class ViewController : HotfixKnightObject
    {
        public    bool                      IsOpened;
        public    bool                      IsClosed;

        protected Dict<string, ViewModel>   ViewModels;

        public ViewController()
        {
            this.ViewModels = new Dict<string, ViewModel>();
        }

        public void DataBindingConnect(ViewModelContainer rViewModelContainer)
        {
            // 把Event绑定到ViewController里面
            this.BindingEvents(rViewModelContainer);

            // ViewModel和View之间的数据绑定
            this.BindingViewAndViewModels(rViewModelContainer);

            // ListViewModel和View之间的数据绑定
            this.BindingListViewAndViewModels(rViewModelContainer);
        }

        public void DataBindingDisconnect(ViewModelContainer rViewModelContainer)
        {
            var rAllMemberBindings = rViewModelContainer.gameObject.GetComponentsInChildren<MemberBindingAbstract>(true);
            for (int i = 0; i < rAllMemberBindings.Length; i++)
            {
                var rMemberBinding = rAllMemberBindings[i];
                if (rMemberBinding.ViewModelProp == null) continue;

                ViewModel rViewModel = rMemberBinding.ViewModelProp.PropertyOwner as ViewModel;
                if (rViewModel != null)
                {
                    rViewModel.PropChangedHandler -= rMemberBinding.ViewModelPropertyWatcher.PropertyChanged;
                }
                rMemberBinding.OnDestroy();
            }

            var rAllEventBindings = rViewModelContainer.gameObject.GetComponentsInChildren<EventBinding>(true);
            for (int i = 0; i < rAllEventBindings.Length; i++)
            {
                rAllEventBindings[i].OnDestroy();
            }
        }

        /// <summary>
        /// 把ViewModel绑定到ViewController里面
        /// </summary>
        public void BindingViewModels(ViewModelContainer rViewModelContainer)
        {
            for (int i = 0; i < rViewModelContainer.ViewModels.Count; i++)
            {
                var rViewModelDataSource = rViewModelContainer.ViewModels[i];
                ViewModel rViewModel = null;
                Type rViewModelType = Type.GetType(rViewModelDataSource.ViewModelPath);
                if (rViewModelType != null)
                {
                    rViewModel = HotfixReflectAssists.Construct(rViewModelType) as ViewModel;
                }
                if (rViewModel != null)
                {
                    this.AddViewModel(rViewModelDataSource.Key, rViewModel);
                }
                else
                {
                    Debug.LogErrorFormat("Can not find ViewModel {0}.", rViewModelDataSource.ViewModelPath);
                }
            }

            // 指定ViewModel给子类的变量 通过HotfixBinding属性标签绑定
            foreach (var rPair in this.ViewModels)
            {
                ViewModel rViewModel = rPair.Value;

                var rViewModelProp = this.GetType().GetFields(HotfixReflectAssists.flags_public)
                    .Where(prop =>
                    {
                        var rAttrObjs = prop.GetCustomAttributes(typeof(HotfixBindingAttribute), false);
                        if (rAttrObjs == null || rAttrObjs.Length == 0) return false;
                        var rBindingAttr = rAttrObjs[0] as HotfixBindingAttribute;

                        return prop.FieldType.IsSubclassOf(typeof(ViewModel)) &&
                                                           rBindingAttr != null &&
                                                           rBindingAttr.Name.Equals(rPair.Key);
                    }).FirstOrDefault();

                if (rViewModelProp != null)
                {
                    rViewModelProp.SetValue(this, rViewModel);
                }
                else
                {
                    Debug.LogErrorFormat("ViewModel {0} is not define in ViewController({1})", rViewModel.GetType(), this.GetType());
                }
            }
        }

        /// <summary>
        /// 把Event绑定到ViewController里面
        /// </summary>
        private void BindingEvents(ViewModelContainer rViewModelContainer)
        {
            for (int i = 0; i < rViewModelContainer.EventBindings.Count; i++)
            {
                var rEventBinding = rViewModelContainer.EventBindings[i];
                var bResult = HotfixDataBindingTypeResolve.MakeViewModelDataBindingEvent(this, rEventBinding);
                if (!bResult)
                {
                    Debug.LogErrorFormat("Make view model binding event {0} failed..", rEventBinding.ViewModelMethod);
                }
            }
        }

        /// <summary>
        /// ViewModel和View之间的数据绑定
        /// </summary>
        private void BindingViewAndViewModels(ViewModelContainer rViewModelContainer)
        {
            var rAllMemberBindings = rViewModelContainer.gameObject.GetComponentsInChildren<MemberBindingAbstract>(true);
            for (int i = 0; i < rAllMemberBindings.Length; i++)
            {
                var rMemberBinding = rAllMemberBindings[i];
                if (rMemberBinding.IsListTemplate) continue;    // 过滤掉ListTemplate标记得Binding Script

                rMemberBinding.ViewProp = DataBindingTypeResolve.MakeViewDataBindingProperty(rMemberBinding.gameObject, rMemberBinding.ViewPath);
                if (rMemberBinding.ViewProp == null)
                {
                    Debug.LogErrorFormat("View Path: {0} error..", rMemberBinding.ViewPath);
                    return;
                }

                rMemberBinding.ViewModelProp = HotfixDataBindingTypeResolve.MakeViewModelDataBindingProperty(rMemberBinding.ViewModelPath);
                if (rMemberBinding.ViewModelProp == null)
                {
                    Debug.LogErrorFormat("View Model Path: {0} error..", rMemberBinding.ViewModelPath);
                    return;
                }
                ViewModel rViewModel = this.GetViewModel(rMemberBinding.ViewModelProp.PropertyOwnerKey);
                if (rViewModel == null)
                {
                    Debug.LogErrorFormat("View Model: {0} error..", rMemberBinding.ViewModelPath);
                    return;
                }

                rMemberBinding.ViewModelProp.PropertyOwner = rViewModel;
                rMemberBinding.SyncFromViewModel();

                // ViewModel绑定View
                rMemberBinding.ViewModelPropertyWatcher = new DataBindingPropertyWatcher(rViewModel, rMemberBinding.ViewModelProp.PropertyName, () =>
                {
                    rMemberBinding.SyncFromViewModel();
                });
                rViewModel.PropChangedHandler += rMemberBinding.ViewModelPropertyWatcher.PropertyChanged;
                
                // View绑定ViewModel
                var rMemberBindingTwoWay = rMemberBinding as MemberBindingTwoWay;
                if (rMemberBindingTwoWay != null)
                {
                    rMemberBindingTwoWay.InitEventWatcher();
                }
            }
        }

        /// <summary>
        /// ListViewModel和View之间的数据绑定
        /// </summary>
        private void BindingListViewAndViewModels(ViewModelContainer rViewModelContainer)
        {
            var rViewModelDataSources = rViewModelContainer.gameObject.GetComponentsInChildren<ViewModelDataSourceList>(true);
            for (int i = 0; i < rViewModelDataSources.Length; i++)
            {
                var rViewModelDataSource = rViewModelDataSources[i];
                rViewModelDataSource.ViewModelProp = HotfixDataBindingTypeResolve.MakeViewModelDataBindingProperty(rViewModelDataSource.ViewModelPath);
                if (rViewModelDataSource.ViewModelProp == null)
                {
                    Debug.LogErrorFormat("View Model Path: {0} error..", rViewModelDataSource.ViewModelPath);
                    return;
                }
                ViewModel rViewModel = this.GetViewModel(rViewModelDataSource.ViewModelProp.PropertyOwnerKey);
                if (rViewModel == null)
                {
                    Debug.LogErrorFormat("View Model: {0} error..", rViewModelDataSource.ViewModelPath);
                    return;
                }

                rViewModelDataSource.ViewModelProp.PropertyOwner = rViewModel;

                // 绑定Watcher
                rViewModelDataSource.ViewModelPropertyWatcher = new DataBindingPropertyWatcher(rViewModel, rViewModelDataSource.ViewModelProp.PropertyName, () =>
                {
                    // 重新设置List数据时候，改变个数
                    var rListObj1 = (IList)rViewModelDataSource.ViewModelProp.GetValue();
                    rViewModelDataSource.ListView.totalCount = rListObj1 != null ? rListObj1.Count : 0;
                    rViewModelDataSource.ListView.RefillCells();
                });

                // 初始化list
                var rViewModelObj = rViewModelDataSource.ViewModelProp.GetValue();
                var rListObservableObj = rViewModelObj as IObservableEvent;
                rListObservableObj.ChangedHandler += () =>
                {
                    var rListObj2 = (IList)rViewModelDataSource.ViewModelProp.GetValue();
                    var nListCount2 = rListObj2 != null ? rListObj2.Count : 0;
                    
                    var nOldCount = rViewModelDataSource.ListView.totalCount;
                    rViewModelDataSource.ListView.totalCount = nListCount2;
                    if (nListCount2 == nOldCount)
                        rViewModelDataSource.ListView.RefreshCells();
                    else
                        rViewModelDataSource.ListView.RefillCells();
                };

                var rListObj = rViewModelObj as IList;
                var nListCount = rListObj != null ? rListObj.Count : 0;
                rViewModelDataSource.ListView.OnFillCellFunc = (rTrans, nIndex) =>
                {
                    this.OnListViewFillCellFunc(rTrans, nIndex, rListObj);
                };
                rViewModelDataSource.ListView.totalCount = nListCount;
                rViewModelDataSource.ListView.RefillCells();
            }
        }

        private void OnListViewFillCellFunc(Transform rTrans, int nIndex, IList rListObj)
        {
            if (rListObj == null || nIndex >= rListObj.Count) return;

            var rListItem = rListObj[nIndex] as ViewModel;
            if (rListItem == null) return;

            // 清除已有的事件监听
            rListItem.PropChangedHandler = null;

            var rAllMemberBindings = rTrans.GetComponentsInChildren<MemberBindingAbstract>();
            for (int i = 0; i < rAllMemberBindings.Length; i++)
            {
                var rMemberBinding = rAllMemberBindings[i];
                if (!rMemberBinding.IsListTemplate) continue;    // 过滤掉非ListTemplate标记的Binding Script

                if (rMemberBinding.ViewProp == null)
                {
                    rMemberBinding.ViewProp = DataBindingTypeResolve.MakeViewDataBindingProperty(rMemberBinding.gameObject, rMemberBinding.ViewPath);
                }
                if (rMemberBinding.ViewProp == null)
                {
                    Debug.LogErrorFormat("List template View Path: {0} error..", rMemberBinding.ViewPath);
                    return;
                }

                if (rMemberBinding.ViewModelProp == null)
                {
                    rMemberBinding.ViewModelProp = HotfixDataBindingTypeResolve.MakeViewModelDataBindingProperty(rMemberBinding.ViewModelPath);
                    if (rMemberBinding.ViewModelProp == null)
                    {
                        Debug.LogErrorFormat("View Model Path: {0} error..", rMemberBinding.ViewModelPath);
                        return;
                    }
                }
                
                rMemberBinding.ViewModelProp.PropertyOwner = rListItem;
                rMemberBinding.SyncFromViewModel();

                if (rListItem != null)
                {
                    // ViewModel绑定View
                    rMemberBinding.ViewModelPropertyWatcher = new DataBindingPropertyWatcher(rListItem, rMemberBinding.ViewModelProp.PropertyName, () =>
                    {
                        rMemberBinding.SyncFromViewModel();
                    });
                    rListItem.PropChangedHandler += rMemberBinding.ViewModelPropertyWatcher.PropertyChanged;
                }
            }
        }
        
        public ViewModel GetViewModel(string rKey)
        {
            ViewModel rViewModel = null;
            this.ViewModels.TryGetValue(rKey, out rViewModel);
            return rViewModel;
        }

        protected void AddViewModel(string rKey, ViewModel rViewModel)
        {
            this.ViewModels.Add(rKey, rViewModel);
        }

        public void Opening()
        {
            this.IsOpened = true;
            this.OnOpening();
        }

        public void Opened()
        {
            this.OnOpened();
        }

        public void Show()
        {
            this.OnShow();
        }

        public void Hide()
        {
            this.OnHide();
        }

        public void Closing()
        {
            this.IsClosed = true;
            this.OnClosing();
        }

        public void Closed()
        {
            this.OnClosed();
        }

        #region Virtual Function
        protected override async Task OnInitialize()
        {
            await base.OnInitialize();
        }

        protected override void OnUpdate()
        {
        }

        protected override void OnDispose()
        {
            foreach (var rPair in this.ViewModels)
            {
                rPair.Value.PropChangedHandler = null;
                rPair.Value = null;
            }
            this.ViewModels.Clear();
        }

        protected virtual void OnOpening()
        {
        }

        protected virtual void OnOpened()
        {
        }

        protected virtual void OnShow()
        {
        }

        protected virtual void OnHide()
        {
        }

        protected virtual void OnClosing()
        {
        }

        protected virtual void OnClosed()
        {
        }
        #endregion
    }
}
