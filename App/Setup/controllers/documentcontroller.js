(function () {
    'use strict';

    angular
    .module('clConnectControllers')
    .controller('documentcontroller', documentcontroller);

    documentcontroller.$inject = ['$scope', 'setupservice', 'validationservice', 'addfieldmappingmodalcontroller'];

    function documentcontroller($scope, setupservice, validationservice, addfieldmappingmodalcontroller) {
        var vm = this;
        vm.service = setupservice;
        vm.validationService = validationservice;
        vm.addfieldmappingmodalcontroller = addfieldmappingmodalcontroller;
        vm.documentSettingsValid = validationservice.pageValidations.documentSettingsValid;
        vm.pathIsValid = false;
        vm.addFieldMapping = addFieldMapping;
        vm.moveUpFieldMappingItem = moveUpFieldMappingItem;
        vm.moveDownFieldMappingItem = moveDownFieldMappingItem;
        vm.editFieldMappingItem = editFieldMappingItem;
        vm.refreshGrid = refreshGrid;
        //vm.createTestIndexFile = createTestIndexFile;
        vm.deleteFieldMappingItem = deleteFieldMappingItem;
        vm.onIndexFileFormatChange = onIndexFileFormatChange;

        vm.fieldMappingCollectionGridOptions = {
            dataSource: vm.service.configurationModel.campusLogicSection.documentSettings.fieldMappingCollection,
            sortable: false,
            pageable: false,
            resizable: true,
            scrollable: true,
            columns: [
                { field: "fieldPosition", title: "Position", width: "10%" },
                { field: "fieldSize", title: "Size", width: "10%" },
                { field: "dataType", title: "Data Type", width: "10%" },
                { field: "fileFieldName", title: "Field Name", width: "10%" },
                { field: "propertyFieldValue", title: "Property Value", width: "10%" },
                { field: "constantFieldValue", title: "Constant Value", width: "10%" },
                { field: "dbCommandFieldValue", title: "Database Command", width: "10%" },
                { field: "dynamicFieldValue", title: "Dynamic Value", width: "10%" },
                {
                    command: [
                    {
                        template: kendo.template($("#add-field-mapping-collection-template").html())
                    }
                    ],
                    width: "20%"
                }
            ]
        };

        function moveUpFieldMappingItem(dataItem) {
            if (dataItem.fieldPosition > 1) {
                var theList = vm.service.configurationModel.campusLogicSection.documentSettings.fieldMappingCollection;
                var moveDown = theList[dataItem.fieldPosition - 2];
                moveDown.fieldPosition++; 
                dataItem.fieldPosition--;
                theList[dataItem.fieldPosition - 1] = dataItem;
                theList[moveDown.fieldPosition - 1] = moveDown;

                vm.refreshGrid();
            }
        }

        function moveDownFieldMappingItem(dataItem) {
            var theList = vm.service.configurationModel.campusLogicSection.documentSettings.fieldMappingCollection;
            if (dataItem.fieldPosition < theList.length) {
                var moveUp = theList[dataItem.fieldPosition];
                moveUp.fieldPosition--;
                dataItem.fieldPosition++;
                theList[dataItem.fieldPosition - 1] = dataItem;
                theList[moveUp.fieldPosition - 1] = moveUp;

                vm.refreshGrid();
            }
        }


        function deleteFieldMappingItem(dataItem) {
            //update items below the one being deleted
            for (var i = dataItem.fieldPosition; i<vm.service.configurationModel.campusLogicSection.documentSettings.fieldMappingCollection.length; i++) {
                vm.service.configurationModel.campusLogicSection.documentSettings.fieldMappingCollection[i].fieldPosition--;
            }
            vm.service.configurationModel.campusLogicSection.documentSettings.fieldMappingCollection.splice([dataItem.fieldPosition - 1], 1);
            
            vm.refreshGrid();
        }

        function editFieldMappingItem(dataItem) {
            vm.addfieldmappingmodalcontroller.open(
                                                dataItem,
                                                dataItem.fieldPosition,
                                                vm.service.configurationModel.campusLogicSection.documentSettings.fieldMappingCollection,
                                                vm.service.configurationModel.campusLogicSection.eventPropertyValueAvailableProperties)
            .result.then(function () {
                vm.refreshGrid();
            });
        }

        function addFieldMapping() {
            vm.addfieldmappingmodalcontroller.open(
                                                null,
                                                vm.service.configurationModel.campusLogicSection.documentSettings.fieldMappingCollection.length + 1,
                                                vm.service.configurationModel.campusLogicSection.documentSettings.fieldMappingCollection,
                                                vm.service.configurationModel.campusLogicSection.eventPropertyValueAvailableProperties)
                .result.then(function() {
                     vm.refreshGrid();
                });
        }

        function refreshGrid(){
            $('#field-mapping-collection-grid').data('kendoGrid').dataSource.read();
        }

        function onIndexFileFormatChange() {
            if (vm.service.configurationModel.campusLogicSection.documentSettings.indexFileFormat === "xml") {
                vm.service.configurationModel.campusLogicSection.documentSettings.includeHeaderRecord = false;
            }
        }

        //function createTestIndexFile() {
        //    this.validationService.testDocumentSettings(vm.documentForm);
        //    if (this.validationService.pageValidations.documentSettingsValid) {
        //        vm.testDocumentIndexFileModal.open();
        //    }
        //}
    }

})();