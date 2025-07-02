import json
import re
import requests
import os

# Function to split PascalCase into Title Case
def split_pascal_case(name):
    if name.upper() == name:
        return name
    return re.sub(r'(?<!^)(?=[A-Z])', ' ', name)

# Function to create a slug from a string for use in Markdown links
def slugify(name):
    return name.lower().replace(' ', '-')

# Define the groups for quantity types
# These groups are used to organize the generated documentation
# Any quantity not explicitly listed here will be added to the "Other" category.
quantity_groups = {
    "Basic Dimensions": [
        "AmountOfSubstance", "Angle", "Area", "Duration", "Information",
        "Length", "Mass", "Scalar", "SolidAngle", "Volume"
    ],
    "Mechanics": [
        "Acceleration", "BrakeSpecificFuelConsumption", "Compressibility", "Density",
        "DynamicViscosity", "Force", "ForceChangeRate", "ForcePerLength",
        "FuelEfficiency", "Impulse", "Jerk", "KinematicViscosity", "LinearDensity",
        "MassFlow", "MassFlux", "MassFraction", "MassMomentOfInertia", "Pressure",
        "PressureChangeRate", "RotationalAcceleration", "RotationalSpeed",
        "RotationalStiffness", "RotationalStiffnessPerLength", "SpecificFuelConsumption",
        "SpecificVolume", "SpecificWeight", "Speed", "StandardVolumeFlow",
        "Torque", "TorquePerLength", "VolumeFlow", "VolumeFlowPerArea", "VolumePerLength",
        "WarpingMomentOfInertia"
    ],
    "Electrical & Magnetic": [
        "ElectricAdmittance", "ElectricApparentEnergy", "ElectricApparentPower",
        "ElectricCapacitance", "ElectricCharge", "ElectricChargeDensity",
        "ElectricConductance", "ElectricConductivity", "ElectricCurrent",
        "ElectricCurrentDensity", "ElectricCurrentGradient", "ElectricField",
        "ElectricImpedance", "ElectricInductance", "ElectricPotential",
        "ElectricPotentialChangeRate", "ElectricReactance", "ElectricReactiveEnergy",
        "ElectricReactivePower", "ElectricResistance", "ElectricResistivity",
        "ElectricSurfaceChargeDensity", "ElectricSusceptance", "MagneticField",
        "MagneticFlux", "Magnetization", "Permeability", "Permittivity"
    ],
    "Thermal": [
        "CoefficientOfThermalExpansion", "Energy", "EnergyDensity", "Entropy",
        "HeatFlux", "HeatTransferCoefficient", "MolarEnergy", "MolarEntropy",
        "SpecificEnergy", "SpecificEntropy", "Temperature", "TemperatureChangeRate",
        "TemperatureDelta", "TemperatureGradient", "ThermalConductivity",
        "ThermalResistance", "VolumetricHeatCapacity"
    ],
    "Light & Radiation": [
        "AbsorbedDoseOfIonizingRadiation", "DoseAreaProduct", "Illuminance",
        "Irradiance", "Irradiation", "Luminance", "Luminosity", "LuminousFlux",
        "LuminousIntensity", "RadiationEquivalentDose", "RadiationEquivalentDoseRate",
        "RadiationExposure", "Radioactivity", "VitaminA"
    ],
    "Chemical & Material": [
        "Molality", "Molarity", "PorousMediumPermeability", "Turbidity", "MassConcentration", "VolumeConcentration"
    ],
    "Ratios & Levels": [
        "AmplitudeRatio", "Level", "Ratio", "RatioChangeRate", "RelativeHumidity"
    ]
}

# Define file paths relative to the script's execution directory
UNITS_JSON_URL = 'https://raw.githubusercontent.com/angularsen/UnitsNet/master/Common/UnitEnumValues.g.json'
UNIT_DEFINITION_BASE_URL = 'https://raw.githubusercontent.com/angularsen/UnitsNet/master/Common/UnitDefinitions/'
LOCAL_UNITS_JSON_PATH = 'Units.json' 
OUTPUT_MD_PATH = 'Units.md' 
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
SCRIPT_NAME = 'enrich_units_doc.py'

# Download Units.json
print(f"Downloading {UNITS_JSON_URL} to {LOCAL_UNITS_JSON_PATH}...")
response = requests.get(UNITS_JSON_URL)
response.raise_for_status()
with open(LOCAL_UNITS_JSON_PATH, 'w', encoding='utf-8') as f:
    f.write(response.text)
print("Download complete.")

# Read the local Units.json file
with open(LOCAL_UNITS_JSON_PATH, 'r', encoding='utf-8') as f:
    content = f.read()

# Strip comments and parse JSON
start_index = content.find('{')
json_content = content[start_index:]
data = json.loads(json_content)

# Dynamically categorize quantities not explicitly defined in quantity_groups
uncategorized_quantities = []
existing_quantities = set()
for group_quantities in quantity_groups.values():
    existing_quantities.update(group_quantities)

for q_name in data.keys():
    if q_name not in existing_quantities:
        uncategorized_quantities.append(q_name)

if uncategorized_quantities:
    if "Other" not in quantity_groups:
        quantity_groups["Other"] = []
    quantity_groups["Other"].extend(sorted(uncategorized_quantities))

# Generate the Markdown content
with open(OUTPUT_MD_PATH, 'w', encoding='utf-8') as f:
    f.write('# UnitsNet Supported Units\n\n')
    f.write('This document lists all the quantity types and their corresponding units of measurement supported by the UnitsNet library.\n\n')

    f.write('## Table of Contents\n\n')
    for group_name in quantity_groups.keys():
        f.write(f'- [{group_name}](#{slugify(group_name)})\n')
        for quantity_name in sorted(quantity_groups[group_name]):
            f.write(f'  - [{split_pascal_case(quantity_name)}](#{slugify(split_pascal_case(quantity_name))})\n')
    f.write('\n')

    for group_name, quantities_in_group in quantity_groups.items():
        f.write(f'## {group_name}\n\n')
        for quantity_name in sorted(quantities_in_group):
            try:
                url = f'{UNIT_DEFINITION_BASE_URL}{quantity_name}.json'
                response = requests.get(url)
                response.raise_for_status() 
                unit_def = json.loads(response.content.decode('utf-8-sig'))

                f.write(f'### {split_pascal_case(quantity_name)}\n`{quantity_name}`\n\n')
                if unit_def.get('XmlDocSummary'):
                    f.write(f'{unit_def["XmlDocSummary"]}\n\n')
                if unit_def.get('BaseUnit'):
                    f.write(f'**Base Unit**: {unit_def["BaseUnit"]}\n\n')

                f.write('**Units**:\n\n')
                for unit in unit_def['Units']:
                    unit_name = unit['SingularName']
                    
                    en_us_abbrs = []
                    if 'Localization' in unit:
                        for loc_entry in unit['Localization']:
                            if 'Culture' in loc_entry and loc_entry['Culture'] == 'en-US':
                                if 'Abbreviations' in loc_entry:
                                    en_us_abbrs.extend(loc_entry['Abbreviations'])
                                break
                    
                    abbr_str = ', '.join(en_us_abbrs)

                    f.write(f'- {split_pascal_case(unit_name)} (`{unit_name}`)')
                    if abbr_str:
                        f.write(f'. Units: {abbr_str}')
                    if unit.get('XmlDocSummary'):
                        f.write(f': {unit["XmlDocSummary"]}')
                    f.write('\n')
                f.write('\n')
            except (requests.exceptions.RequestException, json.JSONDecodeError) as e:
                print(f"Could not process {quantity_name}: {e}")

print(f"Successfully generated {OUTPUT_MD_PATH}")

# Instructions for use:
# 1. Make sure you have Python installed.
# 2. Install the 'requests' library: pip install requests
# 3. Run this script from your terminal: python generate_units_doc.py
# 4. The Units.md file will be generated in the same directory as this script.