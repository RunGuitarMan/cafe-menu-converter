import json,re,collections,logging,pdfplumber
logging.getLogger('pdfminer').setLevel(logging.ERROR)
def join(lines,comp=False):
 s=''
 for v in lines:
  v=re.sub(r'\s+',' ',v).strip()
  if not v:continue
  if s.endswith('-') and comp:s=s[:-1]+v
  elif s.endswith(('-','/')):s+=v
  else:s+=((' ' if s else '')+v)
 return s

def ingredients(s):
 out=[];start=0;depth=0
 for i,c in enumerate(s):
  if c=='(':depth+=1
  if c==')':depth-=1
  if c==',' and depth==0 and not (i and i+1<len(s) and s[i-1].isdigit() and s[i+1].isdigit()):
   out.append(s[start:i].strip());start=i+1
 out.append(s[start:].strip())
 return [s for s in out if s]

def weight(s):
 s=re.sub(r'\s+','',s).replace('.','')
 if not s:return [],'',''
 q=re.search(r'(\d+)(шт|кус)',s)
 if q:
  g=re.search(r'(\d+)гр',s)
  return [g[1]] if g else [],'гр.' if g else '',q[1]
 m=re.fullmatch(r'([\d/]+)(гр|мл)?',s);assert m,s
 return m[1].split('/'),'мл.' if m[2]=='мл' else 'гр.',''

for kind,filename in [('breakfast','menu-for-humans'),('lunch','menu-for-human-obed')]:
 out=[];d=None;section='';date=''
 def new(page):
  return dict(Date=date,MealType='Завтрак' if kind=='breakfast' else 'Обед',Category=section,Name=[],Composition=[],Weight=[],Price='',Protein='',Fats='',Carbohydrates='',Calories='',Page=page)
 def finish():
  global d
  if d is None:return
  d['Name']=join(d['Name']);d['Composition']=ingredients(join(d['Composition'],True))
  d['Weights'],d['Unit'],d['Quantity']=weight(join(d.pop('Weight')))
  if all(d[k]=='0' for k in ['Protein','Fats','Carbohydrates','Calories']):
   for k in ['Protein','Fats','Carbohydrates','Calories']:d[k]=''
  assert d['Name'] and d['Price'],d
  out.append(d);d=None
 with pdfplumber.open('references/'+filename+'.pdf') as pdf:
  for pn,p in enumerate(pdf.pages,1):
   ch=[c for c in p.chars if c['text'].strip() or c['text']==' ']
   dt=re.search(r'(\d\d)\.(\d\d)\.(\d\d)',p.extract_text()).groups();date=f'20{dt[2]}-{dt[1]}-{dt[0]}'
   table=kind=='breakfast' and pn%2==0
   for side in ([0] if table else [0,1]):
    groups=collections.defaultdict(list)
    for c in ch:
     size=round(c['size'],1); y=round(c['bottom'],1)
     if not 105<y<815 or size not in [9,10.5,11,12]:continue
     if not table and (c['x0']>=p.width/2)!=bool(side):continue
     if table and size==9:
      x=c['x0']; col=0 if x<365 else 1 if x<416 else 2 if x<447 else 3 if x<478 else 4 if x<509 else 5
     else:col=0
     groups[y,size,col].append(c)
    for (y,size,col),cs in sorted(groups.items(),key=lambda t:(t[0][0],0 if t[0][1]==10.5 else 1)):
     text=''.join(c['text'] for c in sorted(cs,key=lambda c:c['x0'])).strip();text=re.sub(r'\s+',' ',text)
     if not text:continue
     if size==12:
      if table:finish()
      else:assert d is None, (pn,'unfinished before section',d)
      section=text;continue
     if table and size==10.5:finish()
     if d is None:d=new(pn)
     if size==11:d['Name'].append(text)
     elif size==10.5:d['Price']=text.replace('₽','').strip()
     elif table:
      if col==0:d['Composition'].append(text)
      elif col==1:d['Weight'].append(text)
      else:d[['Protein','Fats','Carbohydrates','Calories'][col-2]]=text
     elif text.startswith('Б:'):
      nums=re.findall(r'\d+',text);assert len(nums)==4,text
      for k,v in zip(['Protein','Fats','Carbohydrates','Calories'],nums):d[k]=v
      finish()
     elif min(c['x0'] for c in cs)>side*p.width/2+190:d['Weight'].append(text)
     else:d['Composition'].append(text)
   if table:finish()
 finish()
 actual=json.load(open('artifacts/review/'+kind+'-result.json'))['Items']
 assert len(actual)==len(out),(len(actual),len(out))
 diff=[]
 for i,(a,b) in enumerate(zip(actual,out)):
  for k in a:
   if a[k]!=b[k]:diff.append((i,k,a[k],b[k]))
 print(kind,'rows',len(out),'differences',len(diff))
 for v in diff[:30]:print(v)
 assert not diff, 'Independent PDF audit failed'
 json.dump(out,open('artifacts/review/independent-'+kind+'.json','w'),ensure_ascii=False,indent=2)
