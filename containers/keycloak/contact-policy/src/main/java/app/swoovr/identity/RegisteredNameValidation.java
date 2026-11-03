package app.swoovr.identity;
import java.text.Normalizer;

public final class RegisteredNameValidation {
    private RegisteredNameValidation() { }
    public static boolean part(String value) {
        if(value==null||value.isBlank()||value.length()>100)return false;
        boolean letter=false;
        for(int point:Normalizer.normalize(value,Normalizer.Form.NFC).codePoints().toArray()) {
            if(Character.isLetter(point)){letter=true;continue;}
            int type=Character.getType(point);
            if(type==Character.NON_SPACING_MARK||type==Character.COMBINING_SPACING_MARK||type==Character.ENCLOSING_MARK||type==Character.DASH_PUNCTUATION||type==Character.SPACE_SEPARATOR)continue;
            if(point==32||point==39||point==45||point==46||point==0x2018||point==0x2019||point==0x05f3||point==0x05f4||point==0x200c||point==0x200d)continue;
            return false;
        }
        return letter;
    }
    public static boolean valid(String first,String last,boolean mononym){return part(first)&&(mononym?(last==null||last.isBlank()):part(last));}
}
